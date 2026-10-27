using Microsoft.EntityFrameworkCore;
using ZU.Training.Application.Reporting;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;
using ZU.Training.Infrastructure.Data;

namespace ZU.Training.Infrastructure.Repositories;

public sealed class EfStatisticsStore(TrainingDbContext db) : IStatisticsStore
{
    private sealed record CourseAggregate(int Lectures, int Enrollments, int Present)
    {
        public double Rate => Lectures <= 0 || Enrollments <= 0
            ? 0
            : Math.Round((double)Present / (Lectures * Enrollments) * 100, 1);
    }

    public async Task<StatisticsOverview> OverviewAsync(Guid? facultyId, CancellationToken ct)
    {
        var courses = db.Courses.AsNoTracking()
            .Where(x => !facultyId.HasValue || x.FacultyId == facultyId);
        var courseIds = await courses.Select(x => x.Id).ToListAsync(ct);
        var aggregates = await AggregatesAsync(courseIds, ct);

        var enrollments = aggregates.Values.Sum(x => x.Enrollments);
        var lectures = aggregates.Values.Sum(x => x.Lectures);
        var present = aggregates.Values.Sum(x => x.Present);

        var certificates = await CertificatesAsync(courseIds, ct);
        var examResults = await db.ExamResults.AsNoTracking()
            .Where(x => x.IsActive && db.Exams.Any(e => e.Id == x.ExamId && courseIds.Contains(e.CourseId)))
            .GroupBy(x => x.Outcome).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        var responses = await (from response in db.EvaluationResponses.AsNoTracking()
                               join enrollment in db.CourseEnrollments.AsNoTracking()
                                   on response.EnrollmentId equals enrollment.Id
                               where response.IsActive && courseIds.Contains(enrollment.CourseId)
                               select response.Id).CountAsync(ct);

        return new StatisticsOverview(
            courseIds.Count,
            await courses.CountAsync(x => x.IsActive && x.Status != CourseStatus.Cancelled, ct),
            await courses.CountAsync(x => x.Status == CourseStatus.Ended, ct),
            await courses.CountAsync(x => x.Status == CourseStatus.Cancelled, ct),
            await db.UserAccounts.CountAsync(x => x.IsActive && !x.IsExternalTrainer &&
                (!facultyId.HasValue || x.FacultyId == facultyId), ct),
            enrollments, lectures,
            certificates.Count(x => x.Status == CertificateStatus.Valid),
            certificates.Count(x => x.Status == CertificateStatus.Revoked),
            responses,
            examResults.FirstOrDefault(x => x.Key == ExamOutcome.Passed)?.Count ?? 0,
            examResults.FirstOrDefault(x => x.Key == ExamOutcome.Failed)?.Count ?? 0,
            lectures <= 0 || enrollments <= 0 ? 0
                : Math.Round((double)present / (lectures * enrollments) * 100, 1));
    }

    public async Task<IReadOnlyList<FacultyRow>> FacultyRowsAsync(CancellationToken ct)
    {
        var faculties = await db.Faculties.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.NameAr).ToListAsync(ct);
        var rows = new List<FacultyRow>();
        foreach (var faculty in faculties)
        {
            var courseIds = await db.Courses.AsNoTracking().Where(x => x.FacultyId == faculty.Id)
                .Select(x => x.Id).ToListAsync(ct);
            var aggregates = await AggregatesAsync(courseIds, ct);
            var certificates = await CertificatesAsync(courseIds, ct);
            var lectures = aggregates.Values.Sum(x => x.Lectures);
            var enrollments = aggregates.Values.Sum(x => x.Enrollments);
            var present = aggregates.Values.Sum(x => x.Present);
            rows.Add(new FacultyRow(faculty.Id, faculty.NameAr, faculty.NameEn, courseIds.Count, enrollments,
                certificates.Count(x => x.Status == CertificateStatus.Valid),
                lectures <= 0 || enrollments <= 0 ? 0
                    : Math.Round((double)present / (lectures * enrollments) * 100, 1)));
        }
        var generalIds = await db.Courses.AsNoTracking().Where(x => x.FacultyId == null)
            .Select(x => x.Id).ToListAsync(ct);
        var generalAggregates = await AggregatesAsync(generalIds, ct);
        var generalCertificates = await CertificatesAsync(generalIds, ct);
        var generalLectures = generalAggregates.Values.Sum(x => x.Lectures);
        var generalEnrollments = generalAggregates.Values.Sum(x => x.Enrollments);
        var generalPresent = generalAggregates.Values.Sum(x => x.Present);
        rows.Add(new FacultyRow(null, "دورات عامة", "General courses", generalIds.Count, generalEnrollments,
            generalCertificates.Count(x => x.Status == CertificateStatus.Valid),
            generalLectures <= 0 || generalEnrollments <= 0 ? 0
                : Math.Round((double)generalPresent / (generalLectures * generalEnrollments) * 100, 1)));
        return rows;
    }

    public async Task<IReadOnlyList<TrackRow>> TrackRowsAsync(CancellationToken ct)
    {
        var tracks = await db.TrainingTracks.AsNoTracking().Where(x => x.IsActive)
            .OrderBy(x => x.NameAr).ToListAsync(ct);
        var rows = new List<TrackRow>();
        foreach (var track in tracks)
        {
            var courseIds = await db.Courses.AsNoTracking().Where(x => x.TrainingTrackId == track.Id)
                .Select(x => x.Id).ToListAsync(ct);
            var aggregates = await AggregatesAsync(courseIds, ct);
            var certificates = await CertificatesAsync(courseIds, ct);
            rows.Add(new TrackRow(track.Id, track.NameAr, track.NameEn, courseIds.Count,
                aggregates.Values.Sum(x => x.Enrollments),
                certificates.Count(x => x.Status == CertificateStatus.Valid)));
        }
        return rows;
    }

    public async Task<IReadOnlyList<CourseRow>> CourseRowsAsync(Guid? facultyId, CancellationToken ct)
    {
        var courses = await db.Courses.AsNoTracking()
            .Where(x => !facultyId.HasValue || x.FacultyId == facultyId)
            .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
        var courseIds = courses.Select(x => x.Id).ToList();
        var aggregates = await AggregatesAsync(courseIds, ct);
        var certificates = await CertificatesAsync(courseIds, ct);
        var examResults = await (from result in db.ExamResults.AsNoTracking()
                                 join exam in db.Exams.AsNoTracking() on result.ExamId equals exam.Id
                                 where result.IsActive && courseIds.Contains(exam.CourseId)
                                 select new { exam.CourseId, result.Outcome }).ToListAsync(ct);
        var responses = await (from response in db.EvaluationResponses.AsNoTracking()
                               join enrollment in db.CourseEnrollments.AsNoTracking()
                                   on response.EnrollmentId equals enrollment.Id
                               where response.IsActive && courseIds.Contains(enrollment.CourseId)
                               group response by enrollment.CourseId
                               into grouped
                               select new { CourseId = grouped.Key, Count = grouped.Count() }).ToListAsync(ct);

        return courses.Select(course =>
        {
            var aggregate = aggregates.GetValueOrDefault(course.Id) ?? new CourseAggregate(0, 0, 0);
            return new CourseRow(course.Id, course.TitleAr, course.TitleEn, (int)course.Status, course.HasExam,
                aggregate.Lectures, aggregate.Enrollments,
                certificates.Count(x => x.CourseId == course.Id && x.Status == CertificateStatus.Valid),
                examResults.Count(x => x.CourseId == course.Id && x.Outcome == ExamOutcome.Passed),
                examResults.Count(x => x.CourseId == course.Id && x.Outcome == ExamOutcome.Failed),
                responses.FirstOrDefault(x => x.CourseId == course.Id)?.Count ?? 0,
                aggregate.Rate, course.EndedAtUtc);
        }).ToList();
    }

    public async Task<IReadOnlyList<MemberRow>> MemberRowsAsync(Guid? facultyId, int take, CancellationToken ct)
    {
        var users = await db.UserAccounts.AsNoTracking()
            .Where(x => x.IsActive && !x.IsExternalTrainer &&
                (!facultyId.HasValue || x.FacultyId == facultyId))
            .OrderBy(x => x.NameAr).Take(take * 10).ToListAsync(ct);
        var enrollments = await db.CourseEnrollments.AsNoTracking().Where(x => x.IsActive)
            .Select(x => new { x.Id, x.UserId, x.CourseId }).ToListAsync(ct);
        var certificates = await (from certificate in db.Certificates.AsNoTracking()
                                  join enrollment in db.CourseEnrollments.AsNoTracking()
                                      on certificate.EnrollmentId equals enrollment.Id
                                  where certificate.IsActive && certificate.Status == CertificateStatus.Valid
                                  select enrollment.UserId).ToListAsync(ct);
        var attendance = await (from record in db.AttendanceRecords.AsNoTracking()
                                join lecture in db.Lectures.AsNoTracking() on record.LectureId equals lecture.Id
                                where record.IsActive && record.IsPresent && lecture.IsActive
                                select new { record.EnrollmentId, lecture.CourseId }).ToListAsync(ct);
        var lectureCounts = await db.Lectures.AsNoTracking().Where(x => x.IsActive)
            .GroupBy(x => x.CourseId).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        var attendanceByEnrollment = attendance.GroupBy(x => x.EnrollmentId)
            .ToDictionary(x => x.Key, x => x.Count());

        return users.Select(user =>
        {
            var own = enrollments.Where(x => x.UserId == user.Id).ToList();
            var expected = own.Sum(x => lectureCounts.FirstOrDefault(l => l.Key == x.CourseId)?.Count ?? 0);
            var present = own.Sum(x => attendanceByEnrollment.GetValueOrDefault(x.Id));
            return new MemberRow(user.Id, user.NameAr, user.NameEn, user.LoginName, own.Count,
                certificates.Count(x => x == user.Id),
                expected <= 0 ? 0 : Math.Round((double)present / expected * 100, 1));
        }).OrderByDescending(x => x.Certificates).ThenBy(x => x.NameAr).Take(take).ToList();
    }

    public async Task<EvaluationReport> EvaluationAsync(Guid? courseId, Guid? versionId, CancellationToken ct)
    {
        var version = versionId.HasValue
            ? await db.EvaluationFormVersions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == versionId, ct)
            : await db.EvaluationFormVersions.AsNoTracking().OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        if (version is null)
            return new EvaluationReport(null, 0, 0, [], 0);

        var questions = await db.EvaluationQuestions.AsNoTracking()
            .Where(x => x.FormVersionId == version.Id && x.IsActive)
            .OrderBy(x => x.Position).ToListAsync(ct);
        var options = await db.EvaluationOptions.AsNoTracking()
            .Where(x => questions.Select(q => q.Id).Contains(x.QuestionId) && x.IsActive)
            .OrderBy(x => x.Position).ToListAsync(ct);

        var answersQuery = from answer in db.EvaluationAnswers.AsNoTracking()
                           join response in db.EvaluationResponses.AsNoTracking()
                               on answer.ResponseId equals response.Id
                           where answer.IsActive && response.IsActive && response.FormVersionId == version.Id
                           select new { answer.QuestionId, answer.OptionId, response.EnrollmentId };
        var answers = await answersQuery.ToListAsync(ct);
        if (courseId.HasValue)
        {
            var enrollmentIds = await db.CourseEnrollments.AsNoTracking()
                .Where(x => x.CourseId == courseId && x.IsActive).Select(x => x.Id).ToListAsync(ct);
            answers = answers.Where(x => enrollmentIds.Contains(x.EnrollmentId)).ToList();
        }

        var rows = new List<QuestionRow>();
        double satisfactionSum = 0;
        var satisfactionCount = 0;
        foreach (var question in questions)
        {
            var questionOptions = options.Where(x => x.QuestionId == question.Id).ToList();
            if (questionOptions.Count == 0) continue;
            var questionAnswers = answers.Where(x => x.QuestionId == question.Id).ToList();
            var optionRows = questionOptions.Select(option =>
            {
                var count = questionAnswers.Count(x => x.OptionId == option.Id);
                return new OptionRow(option.Id, option.Position, option.TextAr, option.TextEn, count,
                    questionAnswers.Count == 0 ? 0
                        : Math.Round((double)count / questionAnswers.Count * 100, 1));
            }).ToList();
            foreach (var answer in questionAnswers)
            {
                var option = questionOptions.FirstOrDefault(x => x.Id == answer.OptionId);
                if (option is null) continue;
                satisfactionSum += (double)option.Position / questionOptions.Count * 100;
                satisfactionCount++;
            }
            rows.Add(new QuestionRow(question.Id, version.Version, question.TextAr, question.TextEn,
                questionAnswers.Count, optionRows));
        }

        var responses = answers.Select(x => x.EnrollmentId).Distinct().Count();
        return new EvaluationReport(version.Id, version.Version, responses, rows,
            satisfactionCount == 0 ? 0 : Math.Round(satisfactionSum / satisfactionCount, 1));
    }

    private async Task<Dictionary<Guid, CourseAggregate>> AggregatesAsync(IReadOnlyCollection<Guid> courseIds,
        CancellationToken ct)
    {
        if (courseIds.Count == 0) return [];
        var lectures = await db.Lectures.AsNoTracking()
            .Where(x => x.IsActive && courseIds.Contains(x.CourseId))
            .GroupBy(x => x.CourseId).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        var enrollments = await db.CourseEnrollments.AsNoTracking()
            .Where(x => x.IsActive && courseIds.Contains(x.CourseId))
            .GroupBy(x => x.CourseId).Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct);
        var present = await (from record in db.AttendanceRecords.AsNoTracking()
                             join lecture in db.Lectures.AsNoTracking() on record.LectureId equals lecture.Id
                             where record.IsActive && record.IsPresent && lecture.IsActive &&
                                   courseIds.Contains(lecture.CourseId)
                             group lecture by lecture.CourseId
                             into grouped
                             select new { CourseId = grouped.Key, Count = grouped.Count() }).ToListAsync(ct);
        return courseIds.ToDictionary(id => id, id => new CourseAggregate(
            lectures.FirstOrDefault(x => x.Key == id)?.Count ?? 0,
            enrollments.FirstOrDefault(x => x.Key == id)?.Count ?? 0,
            present.FirstOrDefault(x => x.CourseId == id)?.Count ?? 0));
    }

    private async Task<List<Certificate>> CertificatesAsync(IReadOnlyCollection<Guid> courseIds,
        CancellationToken ct)
        => courseIds.Count == 0
            ? []
            : await db.Certificates.AsNoTracking()
                .Where(x => x.IsActive && courseIds.Contains(x.CourseId)).ToListAsync(ct);
}
