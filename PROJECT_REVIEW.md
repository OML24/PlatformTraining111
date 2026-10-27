# مراجعة المشروع — منصة التدريب والتطوير المهني، جامعة الزرقاء

> مراجعة محدَّثة على الفرع `arena/63debce2-platformtraining111`.
> تهدف إلى بيان ما نُفّذ فعلياً، وما لم يُتحقَّق منه بعد، والفجوات المتبقية قبل النشر.
> المرجع الأعلى هو [PLATFORM_SPECIFICATION.md](PLATFORM_SPECIFICATION.md) (نسخة 2.7).

## 1. نظرة عامة

| العنصر | القيمة |
| --- | --- |
| النوع | تطبيق ويب .NET 10 وفق Clean Architecture |
| قاعدة البيانات | PostgreSQL 16 + EF Core 10 (Npgsql 10) |
| ملفات C# | 120 ملفاً (30 ملف هجرة/Designer) |
| الأسطر | ~8.5 ألف سطر كود يدوي + ~39.7 ألف سطر هجرات مولّدة |
| الواجهة | WebPortal: 12 صفحة HTML ثابتة + CSS + JS وحدات، Bootstrap 5 (RTL/LTR) + jQuery/AJAX، بلا Razor |
| API | 12 متحكمًا، كوكي JWT، سياسات صلاحيات، Rate Limiting |
| الاختبارات | مشروع xUnit واحد: 8 حالات (6 Fact/Theory في قواعد النطاق + 2 في تخزين الوثائق) |
| النشر | `Dockerfile` + `docker-compose.yml` + `deploy/ubuntu` (systemd) + `deploy/nginx` |

المشروعات:

- `ZU.Training.Domain` — الكيانات وقواعد الحضور/الاستحقاق والصلاحيات.
- `ZU.Training.Application` — حالات الاستخدام والعقود (خدمة لكل نطاق).
- `ZU.Training.Infrastructure` — EF Core، المستودعات، الأمان (AES-GCM/JWT)، التخزين، البريد، PDF/QR، مزامنة الجامعة.
- `ZU.Training.WebApi` — REST API + المصادقة + السياسات.
- `ZU.Training.WebPortal` — الصفحات الثابتة + وكيل `/api` داخلي.
- `tests/ZU.Training.Domain.Tests` — اختبارات قواعد الأعمال والتخزين.

## 2. خريطة الملفات المهمة

```
src/
├─ Domain/
│  ├─ Common/Entity.cs                   # Id, IsActive, IsArchived, CreatedAt/UpdatedAt
│  ├─ Identity/IdentityEntities.cs       # Faculty, Department, UserAccount, Role, Permission…
│  ├─ Identity/PermissionKeys.cs         # مفاتيح الصلاحيات بما فيها صلاحيات المنصة 0201–0217
│  ├─ Identity/PermissionDecision.cs     # Inherit / Allow / Deny
│  ├─ Training/AttendancePolicy.cs       # حد 75٪ وتقريب لأعلى
│  ├─ Training/AttendanceCodeWindow.cs   # نافذة 5 دقائق لكود الحضور
│  ├─ Training/CertificateEligibility.cs # أسباب عدم الاستحقاق
│  ├─ Training/TrainingEntities.cs       # Course, Lecture, Enrollment, Attendance, Exam, Certificate
│  ├─ Training/CourseContent.cs          # LectureMaterial
│  ├─ Training/QualityRequests.cs        # TrainingNeed, CourseCancellationRequest, OfficialDocument
│  ├─ Training/EvaluationImpactEntities.cs, Members/MemberProfile.cs, Communication/Notification.cs
│  └─ Settings/SettingsEntities.cs       # FieldHint, PasswordPolicy, IntegrationSyncRun, AuditEvent
├─ Application/                          # خدمة + Contracts لكل نطاق
├─ Infrastructure/Data/TrainingDbContext.cs
├─ Infrastructure/Data/Migrations/       # 30 ملفاً (هجرات + Designer)
├─ Infrastructure/Repositories/Ef*Store.cs
├─ Infrastructure/Security/               # AesCredentialCipher, JwtTokenIssuer, EfPermissionAccess
├─ Infrastructure/Storage/                # OfficialDocumentStorage, CertificateTemplateStorage, CertificateDocumentStorage
├─ Infrastructure/Documents/              # CertificatePdfGenerator (QuestPDF), SimplePdfWriter, QrCodeRenderer
├─ Infrastructure/Communication/SmtpEmailSender.cs
├─ WebApi/Program.cs, Bootstrap/, Security/PolicyProvider
├─ WebApi/Controllers/                    # Auth, Courses, Certificates, Evaluation, Impact, MemberPortal
│                                         # (Members/Notifications/Audit), AdminUsers, AdminCourses,
│                                         # QualityRequests, ExternalTrainers, Settings, UniversitySync
├─ WebPortal/Program.cs                   # استضافة ثابتة + وكيل /api
└─ WebPortal/wwwroot/                     # 12 صفحة + js/ + css/app.css + config/ + vendor/
```

## 3. المنفَّذ (متحقَّق من الكود)

**المصادقة والصلاحيات**
- دخول JWT بكوكي `zu_access` (HttpOnly، `Path=/api`، `SameSite=Lax`، `Secure` خارج التطوير).
- `auth_version` داخل التوكن مع تحقق في `OnTokenValidated`؛ تعطيل الحساب أو تغيير كلمة المرور يُبطل الجلسات.
- تغيير كلمة المرور الأولى الإلزامي، «تذكرني» (8 ساعات / 30 يوماً)، Rate limit 10 محاولات/دقيقة/IP على مسارات `auth`.
- سياسات صلاحيات من `PermissionKeys` مع تجاوز فردي (Allow/Deny/Inherit)، وADMIN يملك كل شيء تلقائياً.
- مصفوفة أدوار × صلاحيات قابلة للتعديل من شاشة الإدارة (`/api/admin/roles/{roleKey}/permissions`).

**إدارة المستخدمين والدليل**
- إنشاء المستخدم، تفعيل/تعطيل، إسناد الأدوار، تعيين كلمة المرور، عرض/توليد بيانات الاعتماد مشفّرة.
- إرسال بيانات الاعتماد بالبريد (فردي وجماعي) بحالات `NotSent/Sent/Failed/Uncertain` وتدقيق لكل عملية.
- مزامنة دليل الجامعة (موظفون/دوائر/كليات) وتشغيلاتها المسجّلة.
- دليل الموظفين مع البحث (ILIKE) وعدّ الشهادات السارية لكل عضو.

**الدورات**
- إنشاء/تعديل المسودة، إرسال للمراجعة، اعتماد/إعادة بملاحظات/رفض من الجودة.
- ترشيح مشاركين من الجودة، التسجيل الذاتي، استيراد الدورات التاريخية.
- المحاضرات، كود الحضور ذو النافذة الزمنية، التصحيح اليدوي، مواد المحاضرة (رفع/تنزيل/حذف).
- إنهاء الدورة، شرط الامتحان، رصد النتائج (ناجح/راسب)، موعد التقييم.
- طلبات الإلغاء، الاحتياجات التدريبية، طلب محاضر خارجي وإنشاء حساب له.

**الشهادات**
- حساب الاستحقاق (حضور ≥75٪ + اجتياز الامتحان + التقييم) مع عرض الشروط للمشارك.
- إصدار جماعي/فردي، سحب بسبب مكتوب، تحقق عام بالرقم المرجعي، QR يقود لصفحة التحقق.
- توليد PDF عربي RTL عبر QuestPDF (A4 أفقي + QR) مع بديل مبسّط عند تعذّر الخط.
- بروفايل شهادات العضو، تنزيل الشهادة للمالك والمخوّلين فقط.

**التقييم والأثر**
- نموذج تقييم واحد بنسخ منشورة؛ الإجابات مرتبطة بالنسخة؛ أسئلة بخيارات وإجابة واحدة.
- إحصاءات الدورة تفصل النسخ والأسئلة والخيارات وتُظهر مؤشر الرضا.
- ملفات الأثر PDF/DOCX بنسخ متتالية، اعتماد/إعادة من المحاضر، ومنع إعادة الملف نفسه بمحتواه.

**الإشعارات والتدقيق والوثائق**
- إشعارات داخلية لكل حدث (اعتماد، رفض، ترشيح، شهادة، محاضر خارجي…) مع عدّاد غير المقروء وتحديد الكل كمقروء.
- سجل تدقيق قابل للاستعلام (نوع الكيان، المفتاح، الفاعل، المجال الزمني) مع بث مباشر لآخر الأحداث.
- مستودع وثائق رسمية (PDF/DOCX 5B–20MB) مع بحث وتنزيل مقيّد بالصلاحية.

**الواجهة**
- 12 صفحة: الدخول، تغيير كلمة المرور، التوجيه، المشارك، الملف المهني، المحاضر، الجودة، العميد، الإدارة، الدليل، الإحصاءات، التحقق.
- `js/core.js` موحّد: نداءات API، معالجة 401، تبديل اللغة المحفوظ على الخادم، شريط تنقّل حسب الصلاحيات، إشعارات، تنبيهات، ومنتقي تاريخ/وقت عربي مخصّص.
- ترجمة كاملة: 429 مفتاحاً في كل من `ar` و`en`، ولا مفتاح مفقود في الصفحات.
- كل الأصول محلية (Bootstrap، لا CDN) لتعمل داخل بيئات مقيّدة.

**التشغيل والنشر**
- أمر `--migrate` يطبّق الترحيلات ثم يخرج، وأمر `--bootstrap-admin` لإنشاء أول أدمن.
- `Dockerfile` متعدد المراحل (SDK → ASP.NET runtime + خطوط عربية) يبني الـAPI والبوابة في صورة واحدة.
- `docker-compose.yml`: `db` بصحة، `migrate` يسبق الخدمات، `api`، `portal` بمنفذ منشور، وحجم دائم للتخزين.
- `deploy/ubuntu/install.sh` + وحدتا systemd + إعداد nginx (TLS، تمرير `/api/`، حد رفع 45MB).

## 4. الفجوات ومخاطر ما قبل النشر

| # | البند | الأثر | الحالة |
| --- | --- | --- | --- |
| 1 | لا يوجد .NET SDK ولا PostgreSQL في بيئة العمل الحالية | لم يُتحقّق من البناء أو التشغيل بعد آخر مجموعة تعديلات | **حرج — يلزم `dotnet build` + `dotnet test` على جهاز به SDK** |
| 2 | لقطة نماذج EF (`ModelSnapshot`) لم تُحدَّث لهجرة `PlatformCompletion` | `dotnet ef migrations add` لاحقاً قد يولّد فرقاً زائداً | متوسط — الهجرة نفسها SQL صريح يعمل |
| 3 | البريد غير مهيّأ افتراضياً | كلمات المرور الأولية تُسلَّم يدوياً؛ الحالة `Failed` مسجّلة | مقبول للمرحلة الأولى |
| 4 | مسار `HistoryImport` (Excel) لم يُنفّذ | خارج النطاق (§14) | مقصود |
| 5 | حضور دون اتصال وتنبيهات الغياب والمتصدرون | خارج النطاق (§14) | مقصود |
| 6 | اختبارات التكامل للـAPI/المستودعات | لا تغطية آلية للمسارات | متوسط — الاختبارات الحالية للنطاق فقط |
| 7 | ضبط `Certificate:VerificationBaseUrl` | QR خاطئ إن بقي على localhost | حرج عند النشر — مذكور في README |
| 8 | فحص الالتزام بأسلوب الترميز | غير مُشغَّل (لا SDK) | منخفض |

## 5. خطوات القبول المقترحة

1. على جهاز به .NET 10 وPostgreSQL: `dotnet build ZU.Training.sln -warnaserror` ثم `dotnet test`.
2. `docker compose up -d --build` ثم فتح البوابة، الدخول بحساب الأدمن، وتجربة المسار الكامل: إنشاء دورة → اعتماد → ترشيح → تسجيل → محاضرة وحضور → امتحان → تقييم → إصدار شهادة → تحقق عام.
3. التحقق من PDF العربي (خط `Noto Naskh Arabic` داخل الصورة) وQR على العنوان العام.
4. مراجعة سجل التدقيق بعد كل خطوة من الخطوات أعلاه.
5. تحميل الصور والملفات: 20MB للوثائق الرسمية، 15MB للشهادات، 45MB حد nginx.
