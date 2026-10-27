using ZU.Training.Application.Security;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Training;

public sealed class EvaluationService(IEvaluationStore store, IAuthStore auth, IPermissionAccess permissions,
    CertificateIssuanceService certificates)
{
    public async Task<EvaluationFormView> PublishAsync(Guid actorId, PublishEvaluationInput input, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ManageEvaluation, ct);
        if (input.Questions is null || input.Questions.Count is < 1 or > 50)
            throw new ArgumentException("The form needs 1 to 50 questions.");
        foreach (var question in input.Questions)
        {
            if (string.IsNullOrWhiteSpace(question.TextAr) || question.TextAr.Length > 1000 ||
                question.TextEn?.Length > 1000 || question.Options is null || question.Options.Count is < 2 or > 20 ||
                question.Options.Any(x => string.IsNullOrWhiteSpace(x.TextAr) || x.TextAr.Length > 500 || x.TextEn?.Length > 500))
                throw new ArgumentException("Each question needs Arabic text and 2 to 20 valid options.");
        }
        var latest = await store.LatestVersionAsync(ct);
        var version = new EvaluationFormVersion
        {
            Version = (latest?.Version ?? 0) + 1, PublishedByUserId = actorId
        };
        store.AddVersion(version);
        var view = new List<EvaluationQuestionView>();
        for (var i = 0; i < input.Questions.Count; i++)
        {
            var source = input.Questions[i];
            var question = new EvaluationQuestion
            {
                FormVersionId = version.Id, Position = i + 1,
                TextAr = source.TextAr.Trim(), TextEn = Clean(source.TextEn)
            };
            store.AddQuestion(question);
            var optionViews = new List<EvaluationOptionView>();
            for (var j = 0; j < source.Options.Count; j++)
            {
                var option = new EvaluationOption
                {
                    QuestionId = question.Id, Position = j + 1,
                    TextAr = source.Options[j].TextAr.Trim(), TextEn = Clean(source.Options[j].TextEn)
                };
                store.AddOption(option);
                optionViews.Add(new EvaluationOptionView(option.Id, option.TextAr, option.TextEn));
            }
            view.Add(new EvaluationQuestionView(question.Id, question.TextAr, question.TextEn, optionViews));
        }
        foreach (var course in await store.OpenCoursesAsync(DateTimeOffset.UtcNow, ct))
            course.EvaluationFormVersionId = version.Id;
        await store.SaveAsync(ct);
        return new EvaluationFormView(version.Id, version.Version, false, view);
    }

    public async Task<EvaluationFormView?> LatestAsync(Guid actorId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ManageEvaluation, ct);
        var version = await store.LatestVersionAsync(ct);
        return version is null ? null : await FormAsync(version, false, ct);
    }

    public async Task<EvaluationFormView> CourseFormAsync(Guid actorId, Guid courseId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.SubmitEvaluation, ct);
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        var enrollment = await store.EnrollmentAsync(courseId, actorId, ct);
        if (enrollment?.IsActive != true) throw new UnauthorizedAccessException();
        await EnsureEligibleAsync(course, enrollment, ct);
        var version = await AssignedVersionAsync(course, ct);
        var submitted = await store.ResponseAsync(version.Id, enrollment.Id, ct) is not null;
        if (!submitted && course.EvaluationDeadlineUtc <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("The evaluation period has closed.");
        return await FormAsync(version, submitted, ct);
    }

    public async Task<EvaluationResponse> SubmitAsync(Guid actorId, Guid courseId,
        SubmitEvaluationInput input, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.SubmitEvaluation, ct);
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        var enrollment = await store.EnrollmentAsync(courseId, actorId, ct);
        if (enrollment?.IsActive != true) throw new UnauthorizedAccessException();
        await EnsureEligibleAsync(course, enrollment, ct);
        if (course.EvaluationDeadlineUtc <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("The evaluation period has closed.");
        var version = await AssignedVersionAsync(course, ct);
        if (await store.ResponseAsync(version.Id, enrollment.Id, ct) is not null)
            throw new InvalidOperationException("This version was already submitted.");
        var questions = await store.QuestionsAsync(version.Id, ct);
        if (input.Answers is null || input.Answers.Count != questions.Count ||
            input.Answers.Select(x => x.QuestionId).Distinct().Count() != questions.Count)
            throw new ArgumentException("Answer every question once.");
        foreach (var question in questions)
        {
            var answer = input.Answers.SingleOrDefault(x => x.QuestionId == question.Id);
            if (answer is null || !(await store.OptionsAsync(question.Id, ct)).Any(x => x.Id == answer.OptionId))
                throw new ArgumentException("An answer does not belong to its question.");
        }
        var response = new EvaluationResponse { FormVersionId = version.Id, EnrollmentId = enrollment.Id };
        store.AddResponse(response);
        foreach (var answer in input.Answers)
            store.AddAnswer(new EvaluationAnswer
            {
                ResponseId = response.Id, QuestionId = answer.QuestionId, OptionId = answer.OptionId
            });
        await store.SaveAsync(ct);
        await certificates.ReconcileEnrollmentAsync(courseId, enrollment.Id, actorId, ct);
        return response;
    }

    public async Task<IReadOnlyList<EvaluationVersionSummary>> VersionsAsync(Guid actorId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewEvaluationStatistics, ct);
        return (await store.VersionsAsync(ct))
            .Select(x => new EvaluationVersionSummary(x.Id, x.Version, x.PublishedAtUtc)).ToList();
    }

    public async Task<IReadOnlyList<EvaluationCount>> StatisticsAsync(Guid actorId, Guid courseId,
        Guid? versionId, CancellationToken ct)
    {
        await RequireAsync(actorId, PermissionKeys.ViewEvaluationStatistics, ct);
        var course = await store.CourseAsync(courseId, ct) ?? throw new KeyNotFoundException("Course not found.");
        var roles = (await auth.GetRoleKeysAsync(actorId, ct)).ToHashSet(StringComparer.Ordinal);
        if (!roles.Contains("ADMIN") && !roles.Contains("QA") && course.InstructorUserId != actorId)
            throw new UnauthorizedAccessException();
        var version = versionId.HasValue
            ? await store.VersionAsync(versionId.Value, ct) ?? throw new KeyNotFoundException("Evaluation version not found.")
            : await AssignedVersionAsync(course, ct);
        var answers = await store.AnswersAsync(courseId, version.Id, ct);
        var counts = answers.GroupBy(x => new { x.QuestionId, x.OptionId })
            .ToDictionary(x => (x.Key.QuestionId, x.Key.OptionId), x => x.Count());
        var result = new List<EvaluationCount>();
        foreach (var question in await store.QuestionsAsync(version.Id, ct))
            foreach (var option in await store.OptionsAsync(question.Id, ct))
                result.Add(new EvaluationCount(question.Id, option.Id, question.TextAr, question.TextEn,
                    option.TextAr, option.TextEn,
                    counts.GetValueOrDefault((question.Id, option.Id))));
        return result;
    }

    private async Task EnsureEligibleAsync(Course course, CourseEnrollment enrollment, CancellationToken ct)
    {
        if (course.Status != CourseStatus.Ended)
            throw new InvalidOperationException("Evaluation opens after the course ends.");
        var lectures = await store.LectureCountAsync(course.Id, ct);
        var attended = await store.AttendanceCountAsync(course.Id, enrollment.Id, ct);
        if (lectures == 0 || !AttendancePolicy.IsSatisfied(attended, lectures))
            throw new InvalidOperationException("The attendance requirement is not met.");
        if (course.HasExam && await store.ExamOutcomeAsync(course.Id, enrollment.Id, ct) != ExamOutcome.Passed)
            throw new InvalidOperationException("The exam must be passed first.");
    }

    private async Task<EvaluationFormVersion> AssignedVersionAsync(Course course, CancellationToken ct)
    {
        if (!course.EvaluationFormVersionId.HasValue)
            throw new InvalidOperationException("No evaluation form is published for this course.");
        return await store.VersionAsync(course.EvaluationFormVersionId.Value, ct)
            ?? throw new InvalidOperationException("Evaluation form version is unavailable.");
    }

    private async Task<EvaluationFormView> FormAsync(EvaluationFormVersion version, bool submitted, CancellationToken ct)
    {
        var questions = new List<EvaluationQuestionView>();
        foreach (var question in await store.QuestionsAsync(version.Id, ct))
            questions.Add(new EvaluationQuestionView(question.Id, question.TextAr, question.TextEn,
                (await store.OptionsAsync(question.Id, ct)).Select(x =>
                    new EvaluationOptionView(x.Id, x.TextAr, x.TextEn)).ToList()));
        return new EvaluationFormView(version.Id, version.Version, submitted, questions);
    }

    private async Task RequireAsync(Guid actorId, string permission, CancellationToken ct)
    {
        if (await store.UserAsync(actorId, ct) is not { IsActive: true } ||
            !await permissions.IsAllowedAsync(actorId, permission, ct)) throw new UnauthorizedAccessException();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
