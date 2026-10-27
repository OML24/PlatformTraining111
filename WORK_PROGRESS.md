# حالة العمل — 27 أكتوبر 2026

## الهدف
منصة تدريب جامعة الزرقاء كاملة: WebApi + WebPortal ثابت + PostgreSQL، جاهزة للنشر على Ubuntu. المواصفات الملزِمة في `PLATFORM_SPECIFICATION.md` (v2.7).

## حالة الجاهزية
- **الواجهة (WebPortal): مكتملة.** 12 صفحة، `js/core.js` موحّد، ترجمة 429 مفتاحاً لكل لغة، أصول محلية بالكامل، وكل ملفات JS تمرّ من `node --check`.
- **الـAPI والطبقتان Application/Infrastructure: مكتوبة ومطابَقة للعقود، لكن غير مُتحقَّق من بنائها في هذه البيئة** (لا يوجد .NET SDK ولا PostgreSQL ولا Docker). أي ادعاء بنجاح `dotnet build` أو تشغيل حقيقي غير صحيح حتى يُنفَّذ على جهاز به SDK.
- **النشر: ملفات جاهزة** (`Dockerfile`، `docker-compose.yml`، `.env.example`، `deploy/ubuntu/*`، `deploy/nginx/*`) لم تُجرَّب عملياً بعد.

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
1. **التحقق من البناء**: `dotnet restore` + `dotnet build -warnaserror` + `dotnet test` على جهاز به .NET 10، ثم معالجة أي أخطاء تجميع متبقية (الاحتمال الأكبر في تفاصيل الأنواع بين الطبقات).
2. **تشغيل فعلي**: `docker compose up -d --build` وتجربة المسار الكامل (دورة → اعتماد → ترشيح → تسجيل → حضور → امتحان → تقييم → شهادة → تحقق).
3. تحديث `ModelSnapshot` لهجرة `PlatformCompletion` حتى لا تُولّد هجرات لاحقة فرقاً زائداً.
4. اختبارات تكامل للمسارات المحمية (المصادقة، الصلاحيات، رفع الملفات، إصدار الشهادات).
5. ضبط `Certificate:VerificationBaseUrl` و`Portal:BaseUrl` على العنوان العام قبل الإنتاج.

## معلومات معلقة من المستخدم
- واجهة البريد الجامعي التفصيلية، وربط معرفات الموظفين بالكليات والأقسام، ومعاني حالات الموظفين الإضافية: **لم تصل بعد**.
- إعدادات SMTP أو API البريد: **لاحقاً**.
- قالب PDF الرسمي ومواضع البيانات وQR: **لاحقاً**؛ القالب الحالي مولّد داخلياً وزر رفع قالب الأدمن جاهز.

## ملاحظات مهمة
- لا تطبع أسرار قاعدة البيانات أو مفاتيح AES/JWT؛ تُحفظ في User Secrets أو `/etc/zu-training/zu-training.env` (صلاحية 600) خارج المستودع.
- الترحيلات لا تُطبَّق تلقائياً عند الإقلاع؛ استخدم `--migrate` أو خدمة `migrate` في Compose.
- خارج النطاق صراحةً (§14): استيراد Excel للدورات، الحضور دون اتصال، تنبيهات الغياب، لوحة المتصدرين.
