# حالة العمل — 27 أكتوبر 2026

## الهدف
منصة تدريب جامعة الزرقاء كاملة: WebApi + WebPortal ثابت + PostgreSQL، جاهزة للنشر على Ubuntu. المواصفات الملزِمة في `PLATFORM_SPECIFICATION.md` (v2.7).

## حالة الجاهزية
- **البناء والاختبارات: ناجحة على CI** (`.github/workflows/ci.yml`): `dotnet restore` + `build` + `test` على .NET 10، ثم فحص JavaScript وترجمة الواجهة ومطابقة المسارات.
- **تشغيل حقيقي ناجح على CI**: PostgreSQL 16، تطبيق الترحيلات (`--migrate`)، إنشاء حساب أدمن (`--bootstrap-admin`)، تسجيل دخول، تغيير كلمة المرور الإلزامية، ثم 16 فحص HTTP (إحصاءات، دورات، دليل، إشعارات، صلاحيات، تدقيق، تحقق شهادات، حماية مسارات، وصفحات البوابة مع تمرير `/api`).
- **صورة Docker تُبنى بنجاح** على CI (مرحلة بناء + وقت تشغيل مع خطوط عربية).
- **الواجهة (WebPortal): مكتملة.** 12 صفحة، `js/core.js` موحّد، 429 مفتاح ترجمة لكل لغة، أصول محلية، وكل ملفات JS تمرّ `node --check`.
- **النشر**: `Dockerfile`، `docker-compose.yml`، `.env.example`، `deploy/ubuntu/*` (systemd + install.sh + env)، `deploy/nginx/*`، و`DEPLOY.md`؛ صورة Docker مُتحقَّقة على CI، ونشر Ubuntu جاهز للتجربة على الخادم.

## المنجز في هذه المرحلة (آخر تحديث)
- إكمال الواجهة: صفحات المشارك، الملف المهني، المحاضر، الجودة، العميد، الإدارة، الدليل، الإحصاءات، التحقق، تغيير كلمة المرور.
- `js/core.js` موحّد: نداءات API، إعادة توجيه 401، تبديل اللغة المحفوظ على الخادم، تنقّل حسب الصلاحيات، إشعارات، توستات، ومنتقي تاريخ/وقت عربي مخصّص (`[data-date]` / `[data-datetime]`).
- إضافة 17 صلاحية منصة (0201–0217) مع منح الأدوار الصحيحة في هجرة `PlatformCompletion`.
- جداول جديدة: `notifications`، `lecture_materials`، `member_profiles`، `external_trainer_requests`، مع الفهارس والقيود (تفرّد `MemberProfile.UserId`).
- إعادة كتابة العقود والخدمات المتراكبة: `NotificationService`، `CredentialEmailService`، `AuditService` (IQueryable + ILIKE + حل أسماء الفاعلين)، `MemberDirectoryService`، `StatisticsService`، `ExternalTrainerService`، `CertificateIssuanceService`.
- مستودعات EF مطابقة للعقود: `EfNotificationStore`، `EfAuditStore`، `EfMemberDirectoryStore`، `EfStatisticsStore`، `EfExternalTrainerStore`، `EfUserAdminStore`، `EfCourseAdminStore`، `EfQualityRequestStore`.
- إزالة التجريدات الميتة: `EmailDeliveryStatus` لم يبقَ مستخدماً (المعتمد `EmailOutcome`)، وحُذف `js/app.js` القديم وملفات المستودعات المكرّرة.
- توليد PDF الشهادات (QuestPDF، A4 أفقي، RTL عربي، QR) وتنزيل مقيّد بالصلاحية.
- وكيل `/api` في البوابة أعيد كتابته: تمرير كامل للترويسات والكوكيز والجسم مع دعم التحميلات الكبيرة.
- أمر `--migrate` داخل `Program.cs` ليطبّق الترحيلات ثم يخرج، إلى جانب `--bootstrap-admin`.

## أبرز العمل المتبقي
1. **تجربة المسار الكامل يدوياً** (دورة → اعتماد → ترشيح → تسجيل → حضور → امتحان → تقييم → شهادة → تحقق) على `docker compose`.
2. التحقق من شكل PDF الشهادة العربية على خادم فعلي وضبط `Certificate:FontFamily`.
3. اختبارات تكامل أوسع (رفع الملفات، إصدار الشهادات، سجل التدقيق بعد كل خطوة).
4. ضبط `Certificate:VerificationBaseUrl` و`Portal:BaseUrl` على العنوان العام قبل الإنتاج.
5. `ModelSnapshot` محدَّث الآن؛ أي تغيير لاحق في النموذج يُولَّد عبر `Generate Migration` في Actions.

## معلومات معلقة من المستخدم
- واجهة البريد الجامعي التفصيلية، وربط معرفات الموظفين بالكليات والأقسام، ومعاني حالات الموظفين الإضافية: **لم تصل بعد**.
- إعدادات SMTP أو API البريد: **لاحقاً**.
- قالب PDF الرسمي ومواضع البيانات وQR: **لاحقاً**؛ القالب الحالي مولّد داخلياً وزر رفع قالب الأدمن جاهز.

## ملاحظات مهمة
- لا تطبع أسرار قاعدة البيانات أو مفاتيح AES/JWT؛ تُحفظ في User Secrets أو `/etc/zu-training/zu-training.env` (صلاحية 600) خارج المستودع.
- الترحيلات لا تُطبَّق تلقائياً عند الإقلاع؛ استخدم `--migrate` أو خدمة `migrate` في Compose.
- خارج النطاق صراحةً (§14): استيراد Excel للدورات، الحضور دون اتصال، تنبيهات الغياب، لوحة المتصدرين.
