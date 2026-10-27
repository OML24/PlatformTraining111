# دليل النشر — منصة التدريب والتطوير المهني

يغطي هذا الدليل تشغيل المنصة على Ubuntu (Docker أو systemd) وخطوات التحديث والنسخ الاحتياطي والتشخيص.

## 1. المتطلبات

| العنصر | الحد الأدنى | الموصى به |
| --- | --- | --- |
| المعالج/الذاكرة | 2 vCPU / 4 GB | 4 vCPU / 8 GB |
| القرص | 20 GB | 60 GB (تخزين الشهادات والوثائق) |
| نظام التشغيل | Ubuntu 22.04 | Ubuntu 24.04 |
| .NET (للنشر التقليدي) | 8.0 runtime + aspnetcore | 8.0 SDK للبناء على الخادم |
| PostgreSQL | 15 | 16 |
| nginx | 1.18 | 1.24 + شهادة TLS |

المنافذ: الـAPI لا يُنشر خارجياً إطلاقاً (`127.0.0.1:5080` أو شبكة Compose)، والبوابة فقط هي المعرّضة عبر nginx. البوابة تُمرّر `/api/*` إلى الـAPI على نفس الأصل، لذلك لا حاجة إلى إعداد CORS.

## 2. الإعدادات المطلوبة

| المفتاح | الغرض | ملاحظات |
| --- | --- | --- |
| `ConnectionStrings__TrainingDb` | اتصال PostgreSQL | يُحفظ في ملف بيئة بصلاحية 600 |
| `Security__EncryptionKey` | تشفير كلمات المرور المخزنة | Base64 لـ32 بايت، `openssl rand -base64 32` |
| `Security__JwtSigningKey` | توقيع رموز الجلسة | 32 حرفاً عشوائياً على الأقل |
| `Security__SessionHours` / `RememberDays` | مدة الجلسة | افتراضياً 8 ساعات / 30 يوماً |
| `Portal__BaseUrl` | روابط البريد والإشعارات | العنوان العام |
| `Certificate__VerificationBaseUrl` | رابط التحقق المطبوع في QR | مثال `https://training.zu.edu.jo/verify.html` |
| `Certificate__FontFamily` | خط PDF العربي | داخل الصورة: `Noto Naskh Arabic` |
| `Storage__*Path` | مجلدات التخزين | افتراضياً `/var/lib/zu-training/storage/*` |
| `Email__*` | SMTP | تركه فارغاً يعطّل الإرسال ويسجّل الحالة `Failed` |
| `University__Employees/Departments/Faculties` | واجهات مزامنة الدليل | تُغيّر عند توفر الروابط الرسمية |

> لا تضع أي سر في `appsettings.json` أو في Git. المحلي عبر `dotnet user-secrets`، والإنتاج عبر `/etc/zu-training/zu-training.env` أو `.env` لـCompose.

## 3. النشر بـ Docker

```bash
git clone <repo> zu-training && cd zu-training
cp .env.example .env
# ولّد القيم السرية
sed -i "s|^ENCRYPTION_KEY=.*|ENCRYPTION_KEY=$(openssl rand -base64 32)|" .env
sed -i "s|^JWT_SIGNING_KEY=.*|JWT_SIGNING_KEY=$(openssl rand -hex 32)|" .env
sed -i "s|^POSTGRES_PASSWORD=.*|POSTGRES_PASSWORD=$(openssl rand -base64 24 | tr -d '/+=')|" .env
# اضبط العنوان العام
sed -i "s|^PUBLIC_ORIGIN=.*|PUBLIC_ORIGIN=https://training.zu.edu.jo|" .env

docker compose up -d --build
docker compose ps
docker compose logs -f api
```

ترتيب الإقلاع محكوم بالاعتماديات: `db` (حتى يصبح سليماً) → `migrate` (يطبّق الترحيلات ثم يخرج بنجاح) → `api` و`portal`.

إنشاء أول حساب أدمن:

```bash
docker compose run --rm api bootstrap-admin \
  --login 1001 --name "مدير المنصة" --email admin@zu.edu.jo
```

كلمة المرور تُطبع في المخرجات مرة واحدة، وتُجبر على التغيير عند أول دخول.

وضع المنصة خلف nginx على المضيف: انشر البوابة على منفذ محلي فقط `PORTAL_PORT=8081` ثم استخدم `deploy/nginx/zu-training.conf` مع تعديل `proxy_pass http://127.0.0.1:8081;` لقسم `/` وحذف قسم `/api/` (البوابة تتولى التمرير).

## 4. النشر التقليدي (systemd + nginx)

```bash
sudo bash deploy/ubuntu/install.sh
sudo nano /etc/zu-training/zu-training.env      # أكمل القيم
sudo -u zutraining dotnet /opt/zu-training/api/ZU.Training.WebApi.dll --migrate
sudo -u zutraining dotnet /opt/zu-training/api/ZU.Training.WebApi.dll \
  --bootstrap-admin --login 1001 --name "مدير المنصة" --email admin@zu.edu.jo
sudo systemctl enable --now zu-training-api zu-training-portal
```

| الخدمة | المنفذ | الملف |
| --- | --- | --- |
| API | `127.0.0.1:5080` | `/etc/systemd/system/zu-training-api.service` |
| Portal | `127.0.0.1:5081` | `/etc/systemd/system/zu-training-portal.service` |
| nginx | `443` (عام) | `/etc/nginx/sites-available/zu-training.conf` |

خدمة الـAPI تشغّل `--migrate` في `ExecStartPre`، فإعادة التشغيل تطبّق أي ترحيل جديد تلقائياً قبل الإقلاع.

## 5. التحقق بعد النشر

1. `curl -fsS https://<host>/health` → `{"status":"ok"}` (البوابة).
2. `curl -fsS http://127.0.0.1:5080/api/health` → فحص الـAPI مباشرة من الخادم.
3. الدخول بحساب الأدمن، وتغيير كلمة المرور المفروض.
4. إنشاء دورة → اعتماد → ترشيح مشارك → تسجيل → محاضرة وحضور → امتحان → تقييم → إنهاء الدورة → إصدار الشهادات.
5. تنزيل شهادة PDF والتأكد من ظهور الاسم العربي ورمز QR.
6. مسح QR بهاتف: يجب أن يفتح `verify.html` ويُظهر بيانات صحيحة.
7. فتح سجل التدقيق والتأكد من تسجيل الخطوات أعلاه.

## 6. النسخ الاحتياطي والاستعادة

```bash
# قاعدة البيانات
docker compose exec -T db pg_dump -U zu_training -Fc zu_training > backup-$(date +%F).dump

# الملفات (شهادات، وثائق، قوالب)
docker run --rm -v zu-training_storage:/data -v "$PWD":/backup alpine \
  tar czf /backup/storage-$(date +%F).tgz -C /data .
```

الاستعادة:

```bash
docker compose exec -T db pg_restore -U zu_training -d zu_training --clean --if-exists < backup-YYYY-MM-DD.dump
docker run --rm -v zu-training_storage:/data -v "$PWD":/backup alpine \
  tar xzf /backup/storage-YYYY-MM-DD.tgz -C /data
```

جدولة يومية عبر `cron` والاحتفاظ بـ14 نسخة على مساحة منفصلة عن الخادم.

## 7. التحديث

```bash
git pull
docker compose up -d --build      # يعيد بناء الصور ويشغّل الترحيلات
docker compose ps                 # تأكد من سلامة الحاويات
```

في النشر التقليدي:

```bash
git pull && sudo bash deploy/ubuntu/install.sh
sudo systemctl restart zu-training-api zu-training-portal
```

## 8. التشخيص

| العرض | الفحص |
| --- | --- |
| صفحة بيضاء أو 502 | `docker compose logs portal api`؛ تأكد أن `Api:BaseUrl` يشير إلى `http://api:8080` |
| 401 متكرر | تحقق من `Security:JwtSigningKey` ثابتاً، ومن `Path=/api` للكوكي و`SameSite` |
| خطأ عند تطبيق الترحيلات | `docker compose logs migrate`، وتأكد من `ConnectionStrings` وصلاحيات المستخدم |
| PDF بلا حروف عربية | ثبّت `fonts-noto-naskh-arabic` في الصورة أو في المضيف، واضبط `Certificate:FontFamily` |
| فشل رفع ملف | الحد 20MB للوثائق و15MB للشهادات و45MB في nginx (`client_max_body_size`) |
| بريد لا يُرسل | تُسجَّل الحالة `Failed` في حساب المستخدم وسجل التدقيق؛ تحقق من `Email:*` وقيود SMTP |
| QR يفتح صفحة خاطئة | صحّح `Certificate:VerificationBaseUrl` ثم أعد إصدار الشهادات الجديدة (القديمة تحفظ الرابط في PDF) |

## 9. الصلاحيات على نظام الملفات

```bash
sudo chown -R zutraining:zutraining /var/lib/zu-training
sudo chmod 750 /var/lib/zu-training /var/lib/zu-training/storage
sudo chmod 600 /etc/zu-training/zu-training.env
sudo chown root:root /etc/zu-training/zu-training.env
```
