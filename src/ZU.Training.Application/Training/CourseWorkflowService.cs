using ZU.Training.Domain.Communication;
using System.Security.Cryptography;
using ZU.Training.Application.Communication;
using ZU.Training.Application.Security;
using ZU.Training.Domain.Identity;
using ZU.Training.Domain.Training;

namespace ZU.Training.Application.Training;

public sealed class CourseWorkflowService(ICourseStore store, IAuthStore auth, IPermissionAccess permissions,
    CertificateIssuanceService certificates, NotificationService notifications, ICourseFileStorage files)
{
    private sealed record Actor(UserAccount Account, HashSet<string> Roles)
    {
        public bool IsAdmin => Roles.Contains("ADMIN");
        public bool IsQuality => IsAdmin || Roles.Contains("QA");
        public bool IsDean => Roles.Contains("DEAN");
    }

    private async Task<Actor> GetActorAsync(Guid userId, CancellationToken ct)
    {
        var account = await store.UserAsync(userId, ct);
        if (account is null || !account.IsActive) throw new UnauthorizedAccessException();
        return new Actor(account, (await auth.GetRoleKeysAsync(userId, ct)).ToHashSet(StringComparer.Ordinal));
    }

    private async Task RequireAsync(Actor actor, string key, CancellationToken ct)
    {
        if (!await permissions.IsAllowedAsync(actor.Account.Id, key, ct))
            throw new UnauthorizedAccessException();
    }

    private static void EnsureOperator(Actor actor, Course course)
    {
        if (actor.IsAdmin || course.InstructorUserId == actor.Account.Id ||
            (actor.IsQuality && course.QualityOperated)) return;
        throw new UnauthorizedAccessException();
    }

    public async Task<IReadOnlyList<CourseSummary>> ListAsync(Guid userId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ViewCourses, ct);
        var rows = await store.CoursesAsync(ct);
        var result = new List<CourseSummary>();
        foreach (var course in rows)
        {
            var enrollment = await store.EnrollmentAsync(course.Id, userId, ct);
            var joined = enrollment?.IsActive == true;
            var visible = actor.IsQuality ||
                (actor.IsDean && course.FacultyId == actor.Account.FacultyId && course.FacultyId.HasValue) ||
                course.InstructorUserId == userId || joined ||
                (course.Status is CourseStatus.Approved or CourseStatus.InProgress &&
                 course.EnrollmentMode == EnrollmentMode.SelfRegistration &&
                 (!course.FacultyId.HasValue || course.FacultyId == actor.Account.FacultyId));
            if (visible) result.Add(Summary(course, joined, CanSelfEnroll(actor, course, joined)));
        }
        return result;
    }

    public async Task<IReadOnlyList<TrainingTrack>> TracksAsync(Guid userId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ViewCourses, ct);
        return await store.TracksAsync(ct);
    }

    public async Task<IReadOnlyList<Faculty>> FacultiesAsync(Guid userId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ViewCourses, ct);
        return await store.FacultiesAsync(ct);
    }

    public async Task<IReadOnlyList<DirectoryUser>> SearchUsersAsync(Guid actorId, string query,
        Guid? facultyId, CancellationToken ct)
    {
        var actor = await GetActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.SearchUsers, ct);
        if (actor.IsDean && !actor.IsQuality)
        {
            if (!actor.Account.FacultyId.HasValue ||
                (facultyId.HasValue && facultyId != actor.Account.FacultyId))
                throw new UnauthorizedAccessException();
            facultyId = actor.Account.FacultyId;
        }
        query = query.Trim();
        if (query.Length < 2 || query.Length > 100) return [];
        var users = await store.SearchUsersAsync(query, facultyId, ct);
        return users.Select(x => new DirectoryUser(x.Id, x.LoginName, x.NameAr, x.NameEn, x.FacultyId)).ToList();
    }

    public async Task<CourseDetails> DetailsAsync(Guid userId, Guid courseId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ViewCourses, ct);
        var course = await RequiredCourseAsync(courseId, ct);
        var ownEnrollment = await store.EnrollmentAsync(courseId, userId, ct);
        var ownCollege = actor.IsDean && course.FacultyId.HasValue && course.FacultyId == actor.Account.FacultyId;
        var canBrowse = actor.IsQuality || ownCollege || course.InstructorUserId == userId ||
            ownEnrollment?.IsActive == true || CanSelfEnroll(actor, course, false);
        if (!canBrowse) throw new UnauthorizedAccessException();

        var lectures = await store.LecturesAsync(courseId, ct);
        var records = await store.AttendancesAsync(courseId, ct);
        var enrollmentRows = await store.EnrollmentsAsync(courseId, ct);
        var exam = await store.ExamAsync(courseId, ct);
        IReadOnlyList<ExamResult> results = exam is null ? [] : await store.ExamResultsAsync(exam.Id, ct);
        var visibleRoster = actor.IsQuality || ownCollege || course.InstructorUserId == userId;
        var participants = new List<ParticipantSummary>();
        if (visibleRoster)
        {
            foreach (var enrollment in enrollmentRows)
            {
                var person = await store.UserAsync(enrollment.UserId, ct);
                if (person is null) continue;
                var attended = records.Count(x => x.EnrollmentId == enrollment.Id && x.IsPresent);
                var outcome = results.FirstOrDefault(x => x.EnrollmentId == enrollment.Id)?.Outcome ?? ExamOutcome.NotRecorded;
                participants.Add(new ParticipantSummary(enrollment.Id, enrollment.UserId, person.NameAr,
                    person.NameEn, attended, lectures.Count, outcome));
            }
        }
        var lectureDtos = lectures.Select(lecture =>
        {
            var presence = ownEnrollment is null ? null :
                records.FirstOrDefault(x => x.LectureId == lecture.Id && x.EnrollmentId == ownEnrollment.Id)?.IsPresent;
            var expires = lecture.CodeActivatedAtUtc?.Add(AttendanceCodeWindow.Duration);
            return new LectureSummary(lecture.Id, lecture.TitleAr, lecture.TitleEn, lecture.SequenceNumber,
                lecture.StartsAtUtc, expires > DateTimeOffset.UtcNow, expires, presence, lecture.Mode,
                lecture.OnlineLink);
        }).ToList();
        var myOutcome = ownEnrollment is null ? ExamOutcome.NotRecorded :
            results.FirstOrDefault(x => x.EnrollmentId == ownEnrollment.Id)?.Outcome ?? ExamOutcome.NotRecorded;
        return new CourseDetails(Summary(course, ownEnrollment?.IsActive == true,
                CanSelfEnroll(actor, course, ownEnrollment?.IsActive == true)),
            lectureDtos, participants,
            visibleRoster ? records.Select(x => new ParticipantAttendance(x.LectureId, x.EnrollmentId, x.IsPresent)).ToList() : [],
            exam?.NameAr, exam?.NameEn, myOutcome,
            course.ObjectivesAr, course.ObjectivesEn, course.TargetAudienceAr, course.TargetAudienceEn,
            course.DeliveryMode, course.PlannedHours, course.PlannedStartUtc, course.PlannedEndUtc,
            course.EvaluationDeadlineUtc, course.EndedAtUtc);
    }

    public async Task<Course> CreateAsync(Guid userId, CreateCourseInput input, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.CreateCourses, ct);
        if (!actor.IsQuality && !actor.IsDean) throw new UnauthorizedAccessException();
        if (string.IsNullOrWhiteSpace(input.TitleAr) || input.TitleAr.Length > 500 ||
            input.TitleEn?.Length > 500 || input.ActualInstructorNameAr?.Length > 300 ||
            input.ActualInstructorNameEn?.Length > 300 ||
            string.IsNullOrWhiteSpace(input.ObjectivesAr) || input.ObjectivesAr.Length > 4000 ||
            input.ObjectivesEn?.Length > 4000 || string.IsNullOrWhiteSpace(input.TargetAudienceAr) ||
            input.TargetAudienceAr.Length > 1000 || input.TargetAudienceEn?.Length > 1000 ||
            input.PlannedHours <= 0 || input.PlannedEndUtc <= input.PlannedStartUtc ||
            !Enum.IsDefined(input.DeliveryMode) || !Enum.IsDefined(input.EnrollmentMode))
            throw new ArgumentException("Invalid course title or instructor name.");
        if (!await store.TrackExistsAsync(input.TrainingTrackId, ct))
            throw new ArgumentException("Training track not found.");
        if (actor.IsDean && !actor.IsQuality &&
            (!actor.Account.FacultyId.HasValue || input.FacultyId != actor.Account.FacultyId || input.QualityOperated))
            throw new InvalidOperationException("Dean courses must belong to the dean's college.");
        var deanSubmission = actor.IsDean && !actor.IsQuality;
        if (!deanSubmission && (!input.EvaluationDeadlineUtc.HasValue || input.EvaluationDeadlineUtc <= DateTimeOffset.UtcNow))
            throw new ArgumentException("An evaluation deadline in the future is required.");

        UserAccount? instructor = null;
        if (input.InstructorUserId.HasValue)
        {
            instructor = await store.UserAsync(input.InstructorUserId.Value, ct);
            if (instructor is null || !instructor.IsActive) throw new ArgumentException("Instructor not found.");
            if (actor.IsDean && !actor.IsQuality && instructor.FacultyId != actor.Account.FacultyId)
                throw new InvalidOperationException("A dean can select an instructor only from their college.");
        }
        if (input.QualityOperated && input.InstructorUserId.HasValue)
            throw new ArgumentException("Quality operated courses do not use an instructor account.");
        if (actor.IsDean && !actor.IsQuality && instructor is null)
            throw new InvalidOperationException("External instructor proposals require the official letter and CV workflow.");
        var actualName = instructor?.NameAr ?? input.ActualInstructorNameAr?.Trim();
        if (string.IsNullOrWhiteSpace(actualName)) throw new ArgumentException("Instructor name is required.");
        if (!deanSubmission && instructor is null && !input.QualityOperated)
            throw new ArgumentException("Choose an instructor account or quality operated mode.");

        var course = new Course
        {
            TitleAr = input.TitleAr.Trim(), TitleEn = Clean(input.TitleEn), FacultyId = input.FacultyId,
            TrainingTrackId = input.TrainingTrackId, ObjectivesAr = input.ObjectivesAr.Trim(),
            ObjectivesEn = Clean(input.ObjectivesEn), TargetAudienceAr = input.TargetAudienceAr.Trim(),
            TargetAudienceEn = Clean(input.TargetAudienceEn), DeliveryMode = input.DeliveryMode,
            PlannedHours = input.PlannedHours, PlannedStartUtc = input.PlannedStartUtc,
            PlannedEndUtc = input.PlannedEndUtc,
            CreatedByUserId = userId, InstructorUserId = instructor?.Id,
            ActualInstructorNameAr = actualName, ActualInstructorNameEn = instructor?.NameEn ?? Clean(input.ActualInstructorNameEn),
            QualityOperated = input.QualityOperated, HasExam = input.HasExam, EnrollmentMode = input.EnrollmentMode,
            EvaluationDeadlineUtc = input.EvaluationDeadlineUtc,
            EvaluationFormVersionId = (await store.LatestEvaluationVersionAsync(ct))?.Id,
            Status = deanSubmission ? CourseStatus.Submitted : CourseStatus.Approved
        };
        store.AddCourse(course);
        await store.SaveAsync(ct);
        return course;
    }

    public async Task<Course> ApproveAsync(Guid userId, Guid courseId, DateTimeOffset deadline,
        bool qualityOperated, Guid? instructorUserId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ApproveCourses, ct);
        var course = await RequiredCourseAsync(courseId, ct);
        if (course.Status != CourseStatus.Submitted)
            throw new InvalidOperationException("Course is not awaiting approval.");
        if (deadline <= DateTimeOffset.UtcNow) throw new ArgumentException("Evaluation deadline must be in the future.");
        if (qualityOperated)
        {
            course.QualityOperated = true; course.InstructorUserId = null;
        }
        else if (instructorUserId.HasValue)
        {
            var instructor = await store.UserAsync(instructorUserId.Value, ct);
            if (instructor is null || !instructor.IsActive) throw new ArgumentException("Instructor not found.");
            course.InstructorUserId = instructor.Id; course.QualityOperated = false;
            course.ActualInstructorNameAr = instructor.NameAr;
            course.ActualInstructorNameEn = instructor.NameEn;
        }
        if (!course.QualityOperated && !course.InstructorUserId.HasValue)
            throw new InvalidOperationException("Assign an instructor account or quality operation before approval.");
        course.EvaluationDeadlineUtc = deadline;
        course.EvaluationFormVersionId = (await store.LatestEvaluationVersionAsync(ct))?.Id;
        course.Status = CourseStatus.Approved;
        course.ReviewNoteAr = null;
        await store.SaveAsync(ct); return course;
    }

    public async Task<Course> ReviewProposalAsync(Guid actorId, Guid courseId,
        CourseStatus decision, string noteAr, CancellationToken ct)
    {
        var actor = await GetActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.ApproveCourses, ct);
        if (!actor.IsQuality) throw new UnauthorizedAccessException();
        if (decision is not (CourseStatus.RevisionRequested or CourseStatus.Rejected) ||
            string.IsNullOrWhiteSpace(noteAr) || noteAr.Length > 4000)
            throw new ArgumentException("A valid decision and Arabic reason are required.");
        var course = await RequiredCourseAsync(courseId, ct);
        if (course.Status != CourseStatus.Submitted)
            throw new InvalidOperationException("Only submitted proposals can be reviewed.");
        course.Status = decision;
        course.ReviewNoteAr = noteAr.Trim();
        store.AddReviewDecision(new CourseReviewDecision
        {
            CourseId = courseId, ReviewerUserId = actorId, Decision = decision, NoteAr = course.ReviewNoteAr
        });
        await store.SaveAsync(ct);
        return course;
    }

    public async Task<Course> ResubmitProposalAsync(Guid actorId, Guid courseId,
        CreateCourseInput input, CancellationToken ct)
    {
        var actor = await GetActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.CreateCourses, ct);
        var course = await RequiredCourseAsync(courseId, ct);
        if (course.Status != CourseStatus.RevisionRequested ||
            (!actor.IsAdmin && (course.CreatedByUserId != actorId || !actor.IsDean)))
            throw new UnauthorizedAccessException();
        if (!actor.Account.FacultyId.HasValue || input.FacultyId != actor.Account.FacultyId)
            throw new ArgumentException("The proposal must remain in the dean's college.");
        if (string.IsNullOrWhiteSpace(input.TitleAr) || input.TitleAr.Length > 500 ||
            input.TitleEn?.Length > 500 || string.IsNullOrWhiteSpace(input.ObjectivesAr) ||
            input.ObjectivesAr.Length > 4000 || string.IsNullOrWhiteSpace(input.TargetAudienceAr) ||
            input.TargetAudienceAr.Length > 1000 || input.PlannedHours <= 0 ||
            input.PlannedEndUtc <= input.PlannedStartUtc || input.QualityOperated ||
            !Enum.IsDefined(input.DeliveryMode) || !Enum.IsDefined(input.EnrollmentMode) ||
            !await store.TrackExistsAsync(input.TrainingTrackId, ct))
            throw new ArgumentException("Invalid proposal details.");
        if (!input.InstructorUserId.HasValue)
            throw new InvalidOperationException("An internal instructor is required for this proposal workflow.");
        var instructor = await store.UserAsync(input.InstructorUserId.Value, ct);
        if (instructor is null || !instructor.IsActive || instructor.FacultyId != course.FacultyId)
            throw new ArgumentException("Select an active instructor from this college.");
        course.TitleAr = input.TitleAr.Trim(); course.TitleEn = Clean(input.TitleEn);
        course.TrainingTrackId = input.TrainingTrackId;
        course.ObjectivesAr = input.ObjectivesAr.Trim(); course.ObjectivesEn = Clean(input.ObjectivesEn);
        course.TargetAudienceAr = input.TargetAudienceAr.Trim(); course.TargetAudienceEn = Clean(input.TargetAudienceEn);
        course.DeliveryMode = input.DeliveryMode; course.PlannedHours = input.PlannedHours;
        course.PlannedStartUtc = input.PlannedStartUtc; course.PlannedEndUtc = input.PlannedEndUtc;
        course.EnrollmentMode = input.EnrollmentMode; course.HasExam = input.HasExam;
        course.InstructorUserId = instructor.Id; course.ActualInstructorNameAr = instructor.NameAr;
        course.ActualInstructorNameEn = instructor.NameEn;
        course.Status = CourseStatus.Submitted;
        await store.SaveAsync(ct);
        return course;
    }

    public async Task<Course> ExtendEvaluationAsync(Guid actorId, Guid courseId,
        DateTimeOffset deadline, CancellationToken ct)
    {
        var actor = await GetActorAsync(actorId, ct);
        await RequireAsync(actor, PermissionKeys.ManageEvaluation, ct);
        if (!actor.IsQuality) throw new UnauthorizedAccessException();
        var course = await RequiredCourseAsync(courseId, ct);
        if (course.Status is CourseStatus.Cancelled or CourseStatus.Rejected)
            throw new InvalidOperationException("Evaluation cannot be extended for this course.");
        if (deadline <= DateTimeOffset.UtcNow || deadline <= course.EvaluationDeadlineUtc)
            throw new ArgumentException("Choose a later future deadline.");
        course.EvaluationDeadlineUtc = deadline;
        course.EvaluationFormVersionId = (await store.LatestEvaluationVersionAsync(ct))?.Id;
        await store.SaveAsync(ct);
        return course;
    }

    public async Task<CourseEnrollment> SelfEnrollAsync(Guid userId, Guid courseId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.SelfEnroll, ct);
        var course = await RequiredCourseAsync(courseId, ct);
        var existing = await store.EnrollmentAsync(courseId, userId, ct);
        if (!CanSelfEnroll(actor, course, existing?.IsActive == true))
            throw new InvalidOperationException("Self registration is not available for this course or account.");
        if (existing is null)
        {
            existing = new CourseEnrollment { CourseId = courseId, UserId = userId };
            store.AddEnrollment(existing);
        }
        else existing.IsActive = true;
        await store.SaveAsync(ct); return existing;
    }

    public async Task<CourseEnrollment> SelectParticipantAsync(Guid userId, Guid courseId, Guid participantId,
        CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.SelectParticipants, ct);
        var course = await RequiredCourseAsync(courseId, ct);
        if (course.Status is CourseStatus.Cancelled or CourseStatus.Rejected)
            throw new InvalidOperationException("Cannot enroll in a cancelled or rejected course.");
        var participant = await store.UserAsync(participantId, ct);
        if (participant is null || !participant.IsActive || participant.IsExternalTrainer)
            throw new ArgumentException("Participant account is unavailable.");
        var enrollment = await store.EnrollmentAsync(courseId, participantId, ct);
        if (enrollment is null)
        {
            enrollment = new CourseEnrollment { CourseId = courseId, UserId = participantId };
            store.AddEnrollment(enrollment);
        }
        else enrollment.IsActive = true;
        await store.SaveAsync(ct);
        await notifications.NotifyAsync(participantId, NotificationKinds.CourseApproved,
            "أُضفت إلى دورة تدريبية", "You were added to a course",
            course.TitleAr, course.TitleAr, "/member.html?course=" + courseId, ct);
        return enrollment;
    }

    public async Task<Lecture> AddLectureAsync(Guid userId, Guid courseId, string titleAr, string? titleEn,
        DateTimeOffset? startsAtUtc, CancellationToken ct)
        => await AddLectureAsync(userId, courseId, titleAr, titleEn, startsAtUtc, DeliveryMode.InPerson, null, ct);

    public async Task<Lecture> AddLectureAsync(Guid userId, Guid courseId, string titleAr, string? titleEn,
        DateTimeOffset? startsAtUtc, DeliveryMode mode, string? onlineLink, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ManageLectures, ct);
        var course = await RequiredCourseAsync(courseId, ct); EnsureOperator(actor, course);
        if (course.Status is not (CourseStatus.Approved or CourseStatus.InProgress))
            throw new InvalidOperationException("Cannot add a lecture in this course state.");
        if (string.IsNullOrWhiteSpace(titleAr) || titleAr.Length > 300 || titleEn?.Length > 300)
            throw new ArgumentException("Invalid lecture title.");
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Invalid delivery mode.");
        if (onlineLink is { Length: > 1000 }) throw new ArgumentException("Online link is too long.");
        if (mode == DeliveryMode.Online && string.IsNullOrWhiteSpace(onlineLink))
            throw new ArgumentException("An online lecture requires a session link.");
        var lectures = await store.LecturesAsync(courseId, ct);
        var lecture = new Lecture
        {
            CourseId = courseId, TitleAr = titleAr.Trim(), TitleEn = Clean(titleEn), StartsAtUtc = startsAtUtc,
            SequenceNumber = lectures.Count + 1, Mode = mode, OnlineLink = Clean(onlineLink),
            AttendanceCode = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4")
        };
        store.AddLecture(lecture); course.Status = CourseStatus.InProgress;
        await store.SaveAsync(ct);

        var enrolled = await store.EnrollmentsAsync(courseId, ct);
        await notifications.NotifyManyAsync(enrolled.Select(x => x.UserId), NotificationKinds.LectureAdded,
            "أُضيفت محاضرة جديدة", "A new lecture was added",
            course.TitleAr + " — " + lecture.TitleAr, course.TitleAr + " — " + lecture.TitleAr,
            "/member.html?course=" + courseId, ct);
        return lecture;
    }

    /// <summary>تعديل موعد أو نمط أو رابط جلسة قائمة (يتطلب صلاحية إدارة المحاضرات).</summary>
    public async Task<Lecture> UpdateLectureAsync(Guid userId, Guid lectureId, DateTimeOffset? startsAtUtc,
        DeliveryMode mode, string? onlineLink, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ManageLectures, ct);
        var lecture = await RequiredLectureAsync(lectureId, ct);
        var course = await RequiredCourseAsync(lecture.CourseId, ct);
        EnsureOperator(actor, course);
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Invalid delivery mode.");
        if (onlineLink is { Length: > 1000 }) throw new ArgumentException("Online link is too long.");
        if (mode == DeliveryMode.Online && string.IsNullOrWhiteSpace(onlineLink))
            throw new ArgumentException("An online lecture requires a session link.");
        lecture.StartsAtUtc = startsAtUtc;
        lecture.Mode = mode;
        lecture.OnlineLink = Clean(onlineLink);
        await store.SaveAsync(ct);
        return lecture;
    }

    // ------------------------------------------------------------ مرفقات المحاضرات

    public async Task<IReadOnlyList<LectureMaterialView>> MaterialsAsync(Guid userId, Guid lectureId,
        CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ViewCourses, ct);
        var lecture = await RequiredLectureAsync(lectureId, ct);
        var course = await RequiredCourseAsync(lecture.CourseId, ct);
        var enrollment = await store.EnrollmentAsync(lecture.CourseId, userId, ct);
        var canBrowse = actor.IsQuality || course.InstructorUserId == userId ||
            enrollment?.IsActive == true || CanSelfEnroll(actor, course, enrollment?.IsActive == true);
        if (!canBrowse) throw new UnauthorizedAccessException();
        return (await store.MaterialsAsync(lectureId, ct)).Select(x => new LectureMaterialView(x.Id,
            x.LectureId, x.OriginalName, x.Sha256, x.CreatedAtUtc)).ToList();
    }

    public async Task<LectureMaterialView> AddMaterialAsync(Guid userId, Guid lectureId, Stream content,
        string originalName, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ManageLectureMaterials, ct);
        var lecture = await RequiredLectureAsync(lectureId, ct);
        var course = await RequiredCourseAsync(lecture.CourseId, ct);
        EnsureOperator(actor, course);
        var saved = await files.SaveAsync(content, originalName, ct);
        var material = new LectureMaterial
        {
            LectureId = lectureId, OriginalName = saved.OriginalName, StorageKey = saved.StorageKey,
            Sha256 = saved.Sha256, UploadedByUserId = userId
        };
        store.AddMaterial(material);
        try { await store.SaveAsync(ct); }
        catch
        {
            await files.DeleteAsync(saved.StorageKey, CancellationToken.None);
            throw;
        }
        return new LectureMaterialView(material.Id, lectureId, material.OriginalName, material.Sha256,
            material.CreatedAtUtc);
    }

    public async Task<CourseDownload> OpenMaterialAsync(Guid userId, Guid materialId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ViewCourses, ct);
        var material = await store.MaterialAsync(materialId, ct)
            ?? throw new KeyNotFoundException("Material not found.");
        var lecture = await RequiredLectureAsync(material.LectureId, ct);
        var course = await RequiredCourseAsync(lecture.CourseId, ct);
        var enrollment = await store.EnrollmentAsync(course.Id, userId, ct);
        var canBrowse = actor.IsQuality || course.InstructorUserId == userId || enrollment?.IsActive == true ||
            CanSelfEnroll(actor, course, enrollment?.IsActive == true);
        if (!canBrowse) throw new UnauthorizedAccessException();
        var stream = await files.OpenReadAsync(material.StorageKey, ct)
            ?? throw new KeyNotFoundException("Material file is missing.");
        return new CourseDownload(stream, material.OriginalName, ContentTypeFor(material.OriginalName));
    }

    private static string ContentTypeFor(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        _ => "application/octet-stream"
    };

    public async Task<Lecture> ActivateCodeAsync(Guid userId, Guid lectureId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ManageLectures, ct);
        var lecture = await RequiredLectureAsync(lectureId, ct);
        var course = await RequiredCourseAsync(lecture.CourseId, ct); EnsureOperator(actor, course);
        if (course.Status is not (CourseStatus.Approved or CourseStatus.InProgress))
            throw new InvalidOperationException("The course is not active.");
        lecture.CodeActivatedAtUtc = DateTimeOffset.UtcNow;
        await store.SaveAsync(ct); return lecture;
    }

    public async Task<Lecture> OperatorLectureAsync(Guid userId, Guid lectureId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ManageLectures, ct);
        var lecture = await RequiredLectureAsync(lectureId, ct);
        var course = await RequiredCourseAsync(lecture.CourseId, ct); EnsureOperator(actor, course);
        return lecture;
    }

    public async Task<AttendanceRecord> EnterCodeAsync(Guid userId, Guid lectureId, string code, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.EnterAttendanceCode, ct);
        var lecture = await RequiredLectureAsync(lectureId, ct);
        var course = await RequiredCourseAsync(lecture.CourseId, ct);
        if (course.Status is not (CourseStatus.Approved or CourseStatus.InProgress))
            throw new InvalidOperationException("The course is not active.");
        var enrollment = await store.EnrollmentAsync(course.Id, userId, ct);
        if (enrollment?.IsActive != true) throw new UnauthorizedAccessException();
        if (!string.Equals(code, lecture.AttendanceCode, StringComparison.Ordinal) ||
            lecture.CodeActivatedAtUtc is null ||
            !AttendanceCodeWindow.IsOpen(lecture.CodeActivatedAtUtc.Value, DateTimeOffset.UtcNow))
            throw new InvalidOperationException("The code is invalid or expired.");
        return await SaveAttendanceAsync(lecture, enrollment, true, false, userId, ct);
    }

    public async Task<AttendanceRecord> SetAttendanceAsync(Guid userId, Guid lectureId, Guid enrollmentId,
        bool present, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.CorrectAttendance, ct);
        var lecture = await RequiredLectureAsync(lectureId, ct);
        var course = await RequiredCourseAsync(lecture.CourseId, ct); EnsureOperator(actor, course);
        var enrollment = await store.EnrollmentByIdAsync(enrollmentId, ct);
        if (enrollment?.IsActive != true || enrollment.CourseId != course.Id)
            throw new ArgumentException("Enrollment is not in this course.");
        return await SaveAttendanceAsync(lecture, enrollment, present, true, userId, ct);
    }

    public async Task<Course> EndAsync(Guid userId, Guid courseId, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.EndCourse, ct);
        var course = await RequiredCourseAsync(courseId, ct); EnsureOperator(actor, course);
        if (course.Status is not (CourseStatus.Approved or CourseStatus.InProgress))
            throw new InvalidOperationException("The course cannot be ended in this state.");
        if ((await store.LecturesAsync(courseId, ct)).Count == 0)
            throw new InvalidOperationException("At least one lecture is required before ending a course.");
        course.Status = CourseStatus.Ended; course.EndedAtUtc = DateTimeOffset.UtcNow;
        await store.SaveAsync(ct);
        await certificates.IssueForCourseCoreAsync(courseId, userId, ct);
        return course;
    }

    public async Task<Exam> CreateExamAsync(Guid userId, Guid courseId, string nameAr, string? nameEn,
        CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.RecordExam, ct);
        var course = await RequiredCourseAsync(courseId, ct); EnsureOperator(actor, course);
        if (!course.HasExam || course.Status is CourseStatus.Cancelled or CourseStatus.Rejected)
            throw new InvalidOperationException("This course has no exam requirement.");
        if (await store.ExamAsync(courseId, ct) is not null)
            throw new InvalidOperationException("Only one exam is allowed for a course.");
        if (string.IsNullOrWhiteSpace(nameAr) || nameAr.Length > 300 || nameEn?.Length > 300)
            throw new ArgumentException("Invalid exam name.");
        var exam = new Exam { CourseId = courseId, NameAr = nameAr.Trim(), NameEn = Clean(nameEn) };
        store.AddExam(exam); await store.SaveAsync(ct); return exam;
    }

    public async Task<Course> ChangeExamRequirementAsync(Guid userId, Guid courseId, bool required,
        CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ChangeExamRequirement, ct);
        if (!actor.IsQuality) throw new UnauthorizedAccessException();
        var course = await RequiredCourseAsync(courseId, ct);
        if (course.Status is CourseStatus.Cancelled or CourseStatus.Rejected)
            throw new InvalidOperationException("The exam requirement cannot be changed for this course.");
        if (course.HasExam == required) return course;
        var exam = await store.ExamAsync(courseId, ct);
        if (!required && exam is not null)
        {
            var results = await store.ExamResultsAsync(exam.Id, ct);
            if (results.Count > 0 && !actor.IsAdmin)
                throw new InvalidOperationException("Only the administrator can remove an exam with recorded results.");
            foreach (var result in results) result.IsActive = false;
            exam.IsActive = false;
        }
        course.HasExam = required;
        await store.SaveAsync(ct);
        await certificates.ReconcileAsync(courseId, userId, ct);
        return course;
    }

    public async Task<ExamResult> RecordExamResultAsync(Guid userId, Guid courseId, Guid enrollmentId,
        ExamOutcome outcome, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.RecordExam, ct);
        var course = await RequiredCourseAsync(courseId, ct); EnsureOperator(actor, course);
        if (!course.HasExam || outcome is not (ExamOutcome.Passed or ExamOutcome.Failed))
            throw new ArgumentException("Select Passed or Failed explicitly.");
        var exam = await store.ExamAsync(courseId, ct) ?? throw new InvalidOperationException("Create the exam first.");
        var enrollment = await store.EnrollmentByIdAsync(enrollmentId, ct);
        if (enrollment?.IsActive != true || enrollment.CourseId != courseId)
            throw new ArgumentException("Enrollment is not in this course.");
        var result = await store.ExamResultAsync(exam.Id, enrollmentId, ct);
        if (result is null)
        {
            result = new ExamResult { ExamId = exam.Id, EnrollmentId = enrollmentId,
                Outcome = outcome, RecordedByUserId = userId };
            store.AddExamResult(result);
        }
        else { result.Outcome = outcome; result.RecordedByUserId = userId; }
        await store.SaveAsync(ct);
        await certificates.ReconcileEnrollmentAsync(courseId, enrollmentId, userId, ct);
        return result;
    }

    private async Task<AttendanceRecord> SaveAttendanceAsync(Lecture lecture, CourseEnrollment enrollment,
        bool present, bool manual, Guid actorId, CancellationToken ct)
    {
        var record = await store.AttendanceAsync(lecture.Id, enrollment.Id, ct);
        if (record is null)
        {
            record = new AttendanceRecord { LectureId = lecture.Id, EnrollmentId = enrollment.Id,
                IsPresent = present, SetByInstructor = manual, LastChangedByUserId = actorId };
            store.AddAttendance(record);
        }
        else
        {
            record.IsPresent = present; record.SetByInstructor = manual; record.LastChangedByUserId = actorId;
            record.IsActive = true;
        }
        await store.SaveAsync(ct);
        await certificates.ReconcileEnrollmentAsync(lecture.CourseId, enrollment.Id, actorId, ct);
        return record;
    }

    /// <summary>
    /// إدخال دورة تاريخية منتهية مع محاضراتها ومشاركيها وتوثيق شروط الشهادة، ثم إصدار الشهادات
    /// للمستحقين فوراً (القسم 12 من المواصفات).
    /// </summary>
    public async Task<Course> CreateHistoricalAsync(Guid userId, HistoricalCourseInput input, CancellationToken ct)
    {
        var actor = await GetActorAsync(userId, ct);
        await RequireAsync(actor, PermissionKeys.ImportHistoricalCourses, ct);
        if (!actor.IsQuality) throw new UnauthorizedAccessException();

        if (string.IsNullOrWhiteSpace(input.TitleAr) || input.TitleAr.Length > 500 || input.TitleEn?.Length > 500 ||
            string.IsNullOrWhiteSpace(input.ObjectivesAr) || input.ObjectivesAr.Length > 4000 ||
            string.IsNullOrWhiteSpace(input.TargetAudienceAr) || input.TargetAudienceAr.Length > 1000 ||
            input.PlannedHours <= 0 || input.EndUtc <= input.StartUtc ||
            !Enum.IsDefined(input.DeliveryMode))
            throw new ArgumentException("Invalid historical course details.");
        if (input.LectureCount is < 1 or > 200)
            throw new ArgumentException("A historical course needs 1 to 200 lectures.");
        if (!await store.TrackExistsAsync(input.TrainingTrackId, ct))
            throw new ArgumentException("Training track not found.");
        var participants = (input.Participants ?? []).ToList();
        if (participants.Count > 2000) throw new ArgumentException("Too many participants.");
        if (participants.Select(x => x.UserId).Distinct().Count() != participants.Count)
            throw new ArgumentException("Duplicate participants.");
        foreach (var row in participants)
        {
            if (row.AttendedLectures < 0 || row.AttendedLectures > input.LectureCount)
                throw new ArgumentException("Attended lectures exceed the course lectures.");
            var user = await store.UserAsync(row.UserId, ct);
            if (user is null || !user.IsActive || user.IsExternalTrainer)
                throw new ArgumentException("A participant account is unavailable.");
        }

        UserAccount? instructor = null;
        if (input.InstructorUserId.HasValue)
        {
            instructor = await store.UserAsync(input.InstructorUserId.Value, ct);
            if (instructor is null || !instructor.IsActive) throw new ArgumentException("Instructor not found.");
        }
        var actualName = instructor?.NameAr ?? Clean(input.ActualInstructorNameAr);
        if (string.IsNullOrWhiteSpace(actualName)) throw new ArgumentException("Instructor name is required.");

        var course = new Course
        {
            TitleAr = input.TitleAr.Trim(), TitleEn = Clean(input.TitleEn), FacultyId = input.FacultyId,
            TrainingTrackId = input.TrainingTrackId, ObjectivesAr = input.ObjectivesAr.Trim(),
            ObjectivesEn = Clean(input.ObjectivesEn), TargetAudienceAr = input.TargetAudienceAr.Trim(),
            TargetAudienceEn = Clean(input.TargetAudienceEn), DeliveryMode = input.DeliveryMode,
            PlannedHours = input.PlannedHours, PlannedStartUtc = input.StartUtc, PlannedEndUtc = input.EndUtc,
            CreatedByUserId = userId, InstructorUserId = instructor?.Id, ActualInstructorNameAr = actualName,
            ActualInstructorNameEn = instructor?.NameEn ?? Clean(input.ActualInstructorNameEn),
            HasExam = input.HasExam, IsHistorical = true,
            EnrollmentMode = input.EnrollmentModeSelfRegistration ? EnrollmentMode.SelfRegistration
                : EnrollmentMode.QualitySelection,
            EvaluationFormVersionId = (await store.LatestEvaluationVersionAsync(ct))?.Id,
            Status = CourseStatus.Ended, EndedAtUtc = input.EndUtc
        };
        store.AddCourse(course);

        var lectures = new List<Lecture>();
        for (var i = 1; i <= input.LectureCount; i++)
        {
            var lecture = new Lecture
            {
                CourseId = course.Id,
                TitleAr = "الجلسة " + i,
                TitleEn = "Session " + i,
                SequenceNumber = i,
                StartsAtUtc = null,
                Mode = input.DeliveryMode,
                AttendanceCode = RandomNumberGenerator.GetInt32(0, 10000).ToString("D4")
            };
            lectures.Add(lecture);
            store.AddLecture(lecture);
        }

        Exam? exam = null;
        if (input.HasExam)
        {
            exam = new Exam
            {
                CourseId = course.Id,
                NameAr = string.IsNullOrWhiteSpace(input.ExamNameAr) ? "امتحان الدورة" : input.ExamNameAr.Trim(),
                NameEn = Clean(input.ExamNameEn)
            };
            store.AddExam(exam);
        }

        foreach (var row in participants)
        {
            var enrollment = new CourseEnrollment
            {
                CourseId = course.Id, UserId = row.UserId,
                LegacyEvaluationConfirmed = row.EvaluationConfirmed,
                LegacyImpactConfirmed = row.ImpactConfirmed
            };
            store.AddEnrollment(enrollment);
            for (var i = 0; i < row.AttendedLectures; i++)
                store.AddAttendance(new AttendanceRecord
                {
                    LectureId = lectures[i].Id, EnrollmentId = enrollment.Id, IsPresent = true,
                    SetByInstructor = true, LastChangedByUserId = userId
                });
            if (exam is not null && row.ExamOutcome is ExamOutcome.Passed or ExamOutcome.Failed)
                store.AddExamResult(new ExamResult
                {
                    ExamId = exam.Id, EnrollmentId = enrollment.Id, Outcome = row.ExamOutcome,
                    RecordedByUserId = userId
                });
        }

        store.AddAudit(new Domain.Settings.AuditEvent
        {
            ActorUserId = userId, ActionKey = "course.historical.created", EntityType = "Course",
            EntityId = course.Id, Reason = $"{input.LectureCount} lectures; {participants.Count} participants"
        });
        await store.SaveAsync(ct);
        await certificates.IssueForCourseCoreAsync(course.Id, userId, ct);
        return course;
    }

    private async Task<Course> RequiredCourseAsync(Guid id, CancellationToken ct)
        => await store.CourseAsync(id, ct) ?? throw new KeyNotFoundException("Course not found.");
    private async Task<Lecture> RequiredLectureAsync(Guid id, CancellationToken ct)
        => await store.LectureAsync(id, ct) ?? throw new KeyNotFoundException("Lecture not found.");

    private static bool CanSelfEnroll(Actor actor, Course course, bool alreadyEnrolled)
        => !alreadyEnrolled && !actor.Account.IsExternalTrainer &&
           course.Status is CourseStatus.Approved or CourseStatus.InProgress &&
           course.EnrollmentMode == EnrollmentMode.SelfRegistration &&
           (!course.FacultyId.HasValue || course.FacultyId == actor.Account.FacultyId);

    private static CourseSummary Summary(Course course, bool enrolled, bool canEnroll)
        => new(course.Id, course.TitleAr, course.TitleEn, course.Status, course.HasExam,
            course.EnrollmentMode, course.QualityOperated, course.ActualInstructorNameAr,
            course.TrainingTrackId, course.FacultyId, course.CreatedByUserId,
            course.InstructorUserId, enrolled, canEnroll, course.ReviewNoteAr,
            course.EvaluationFormVersionId);

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
