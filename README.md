# منصة التدريب والتطوير المهني — جامعة الزرقاء

مشروع .NET 10 وفق Clean Architecture مع PostgreSQL وEF Core. الواجهة (WebPortal) صفحة ثابتة بـHTML5 وBootstrap 5 وjQuery/AJAX، بلا Razor، وتتحدث بالعربية والإنجليزية. مصدر المتطلبات في [PLATFORM_SPECIFICATION.md](PLATFORM_SPECIFICATION.md)، وحالة التنفيذ التفصيلية في [WORK_PROGRESS.md](WORK_PROGRESS.md)، ومراجعة الجاهزية في [PROJECT_REVIEW.md](PROJECT_REVIEW.md).

## المشروعات

| المشروع | المسؤولية |
| --- | --- |
| `ZU.Training.Domain` | الكيانات وقواعد الحضور واستحقاق الشهادة |
| `ZU.Training.Application` | حالات الاستخدام والعقود والخدمات |
| `ZU.Training.Infrastructure` | EF Core، PostgreSQL، التخزين، البريد، توليد PDF |
| `ZU.Training.WebApi` | REST API والمصادقة والصلاحيات |
| `ZU.Training.WebPortal` | الواجهة الثابتة العربية/الإنجليزية + وكيل `/api` |
| `ZU.Training.Domain.Tests` | اختبارات قواعد الأعمال والتخزين |

## التشغيل المحلي

المتطلبات: .NET 10 SDK، PostgreSQL 16.

1. أنشئ قاعدة البيانات والملف السري للمفاتيح:

```bash
# PostgreSQL
sudo -u postgres psql -c "CREATE USER zu_training WITH PASSWORD 'dev-password';"
sudo -u postgres psql -c "CREATE DATABASE zu_lms_db OWNER zu_training;"

# مفاتيح التطوير (تُحفظ في User Secrets ولا تُنسخ إلى المستودع)
cd src/ZU.Training.WebApi
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:TrainingDb" "Host=127.0.0.1;Port=5432;Database=zu_lms_db;Username=zu_training;Password=dev-password"
dotnet user-secrets set "Security:EncryptionKey" "$(openssl rand -base64 32)"
dotnet user-secrets set "Security:JwtSigningKey" "$(openssl rand -hex 32)"
```

2. طبّق الترحيلات ثم شغّل الخدمتين:

```bash
dotnet restore ZU.Training.sln
dotnet tool restore

# ترحيل قاعدة البيانات (أمر مستقل داخل التطبيق)
dotnet run --project src/ZU.Training.WebApi -- --migrate

# حساب الأدمن الأول (مرة واحدة)
dotnet run --project src/ZU.Training.WebApi -- \
  --bootstrap-admin --login 1001 --name "مدير المنصة" --email admin@zu.edu.jo

# الخدمتان في نافذتين
dotnet run --project src/ZU.Training.WebApi --urls http://127.0.0.1:5189
dotnet run --project src/ZU.Training.WebPortal --urls http://127.0.0.1:5249
```

افتح `http://127.0.0.1:5249/`. البوابة تُمرّر كل مسارات `/api` إلى `Api:BaseUrl` (الافتراضي `http://127.0.0.1:5189`)، لذا يبقى ملف تعريف الجلسة `zu_access` صالحاً على نفس الأصل ولا حاجة إلى CORS.

3. الاختبارات:

```bash
dotnet test ZU.Training.sln
```

## الصفحات

| الصفحة | الدور |
| --- | --- |
| `index.html` | الدخول والتحقق من الشهادة |
| `change-password.html` | تغيير كلمة المرور الأولى الإلزامي |
| `dashboard.html` | توجيه المستخدم إلى لوحة دوره |
| `member.html` / `member-profile.html` | لوحة المشارك وملفه المهني وشهاداته |
| `instructor.html` | لوحة المحاضر: المحاضرات، الحضور، التقييم، الأثر |
| `quality.html` | دائرة الجودة: المقترحات، الترشيح، الاحتياجات، الإلغاء |
| `dean.html` | العميد: دورات كليته والتقارير |
| `admin.html` | الإدارة: المستخدمون، الأدوار، الدورات، القوالب، الوثائق، التدقيق |
| `directory.html` | دليل الموظفين |
| `statistics.html` | الإحصاءات |
| `verify.html` | تحقق عام من الشهادة بالرقم المرجعي |

## النشر

### Docker (موصى به للتجربة والاختبار)

```bash
cp .env.example .env          # املأ كلمات المرور والمفاتيح
docker compose up -d --build  # db → migrate → api + portal
```

الخدمات: `db` (PostgreSQL 16)، `migrate` (يطبّق الترحيلات ثم ينتهي)، `api` (المنفذ الداخلي 8080)، `portal` (المنشور على `${PORTAL_PORT}`). البوابة تتصل بالـAPI عبر شبكة Compose الداخلية باسم `http://api:8080`.

```bash
docker compose run --rm api bootstrap-admin --login 1001 --name "مدير المنصة" --email admin@zu.edu.jo
docker compose logs -f api
```

### Ubuntu + systemd + nginx (بدون Docker)

```bash
sudo bash deploy/ubuntu/install.sh
sudo nano /etc/zu-training/zu-training.env     # كلمة مرور القاعدة + المفاتيح
sudo -u zutraining dotnet /opt/zu-training/api/ZU.Training.WebApi.dll --migrate
sudo -u zutraining dotnet /opt/zu-training/api/ZU.Training.WebApi.dll \
  --bootstrap-admin --login 1001 --name "مدير المنصة" --email admin@zu.edu.jo
sudo systemctl enable --now zu-training-api zu-training-portal
```

- `deploy/ubuntu/zu-training-api.service` و`zu-training-portal.service`: خدمتا systemd تعملان بمستخدم `zutraining` على `127.0.0.1:5080` و`5081`.
- `deploy/nginx/zu-training.conf`: TLS، وتمرير `/api/` إلى الـAPI و`/` إلى البوابة، بحد رفع 45MB.
- الملفات المرفوعة والوثائق في `/var/lib/zu-training/storage` (خارج مجلد التطبيق).
- الأسرار في `/etc/zu-training/zu-training.env` بصلاحية `600` فقط، ولا تُخزَّن في المستودع.

## ملاحظات تشغيلية

- ترحيلات قاعدة البيانات تُطبَّق بـ`--migrate` صراحةً (أو `ExecStartPre` في systemd، أو خدمة `migrate` في Compose)؛ التطبيق لا يهاجر تلقائياً عند الإقلاع.
- رقم الشهادة (`Certificate:VerificationBaseUrl`) يُطبع في رمز QR داخل PDF؛ اضبطه على العنوان العام قبل إصدار أي شهادة.
- البريد اختياري: بدون `Email:Host` تُسجَّل محاولات الإرسال كـ`Failed` وتظهر في سجل التدقيق، وتُرسَل كلمات المرور الأولية يدوياً من شاشة الإدارة.
- لا تُفعّل `UseHttpsRedirection`؛ التطبيق يعمل خلف nginx/وكيل يعالج TLS ويقرأ `X-Forwarded-*`.
