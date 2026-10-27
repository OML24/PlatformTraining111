#!/usr/bin/env bash
# تثبيت منصة التدريب على Ubuntu 22.04/24.04 بشكل تقليدي (بدون Docker).
# يُشغَّل من جذر المستودع:  sudo bash deploy/ubuntu/install.sh
set -euo pipefail

APP_USER=zutraining
APP_ROOT=/opt/zu-training
DATA_ROOT=/var/lib/zu-training
CONFIG_DIR=/etc/zu-training
ENV_FILE="$CONFIG_DIR/zu-training.env"

if [[ $EUID -ne 0 ]]; then
  echo "شغّل السكربت بصلاحيات root: sudo bash deploy/ubuntu/install.sh" >&2
  exit 1
fi

command -v dotnet >/dev/null 2>&1 || {
  echo "يلزم تثبيت .NET 10 SDK أولاً (https://learn.microsoft.com/dotnet/core/install/linux-ubuntu)." >&2
  exit 1
}

echo "==> إنشاء المستخدم والمسارات"
id -u "$APP_USER" >/dev/null 2>&1 || useradd --system --home "$APP_ROOT" --shell /usr/sbin/nologin "$APP_USER"
install -d -o "$APP_USER" -g "$APP_USER" "$APP_ROOT" "$DATA_ROOT/storage" \
  "$DATA_ROOT/storage/official-documents" "$DATA_ROOT/storage/certificate-templates" \
  "$DATA_ROOT/storage/certificates" "$DATA_ROOT/storage/course-files" "$CONFIG_DIR"

echo "==> نشر التطبيقات"
dotnet publish src/ZU.Training.WebApi/ZU.Training.WebApi.csproj -c Release -o /tmp/zu-api
dotnet publish src/ZU.Training.WebPortal/ZU.Training.WebPortal.csproj -c Release -o /tmp/zu-portal
rm -rf "$APP_ROOT/api" "$APP_ROOT/portal"
cp -r /tmp/zu-api "$APP_ROOT/api"
cp -r /tmp/zu-portal "$APP_ROOT/portal"
chown -R "$APP_USER:$APP_USER" "$APP_ROOT"

echo "==> إعداد ملف البيئة"
if [[ ! -f "$ENV_FILE" ]]; then
  install -m 600 deploy/ubuntu/zu-training.env.example "$ENV_FILE"
  echo "راجع $ENV_FILE وأكمل كلمات المرور والمفاتيح قبل بدء الخدمتين."
fi

echo "==> تثبيت خدمات systemd"
install -m 644 deploy/ubuntu/zu-training-api.service /etc/systemd/system/zu-training-api.service
install -m 644 deploy/ubuntu/zu-training-portal.service /etc/systemd/system/zu-training-portal.service
systemctl daemon-reload
systemctl enable zu-training-api.service zu-training-portal.service

echo "==> إعداد nginx"
if command -v nginx >/dev/null 2>&1; then
  install -m 644 deploy/nginx/zu-training.conf /etc/nginx/sites-available/zu-training.conf
  ln -sf /etc/nginx/sites-available/zu-training.conf /etc/nginx/sites-enabled/zu-training.conf
  nginx -t && systemctl reload nginx
fi

echo "==> إنشاء قاعدة البيانات (إن لم تكن موجودة)"
sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='zu_training'" | grep -q 1 || {
  sudo -u postgres psql -c "CREATE USER zu_training WITH PASSWORD 'CHANGE_ME';"
  sudo -u postgres psql -c "CREATE DATABASE zu_training OWNER zu_training;"
}

cat <<'DONE'

تم التثبيت. الخطوات المتبقية:
  1) حرّر /etc/zu-training/zu-training.env وأكمل كلمة مرور قاعدة البيانات والمفاتيح:
       openssl rand -base64 32     # Security__EncryptionKey
       openssl rand -hex 32        # Security__JwtSigningKey
  2) طبّق ترحيلات قاعدة البيانات:
       sudo -u zutraining dotnet /opt/zu-training/api/ZU.Training.WebApi.dll --migrate
  3) أنشئ حساب الأدمن الأول:
       sudo -u zutraining dotnet /opt/zu-training/api/ZU.Training.WebApi.dll \
         --bootstrap-admin --login 1001 --name "مدير المنصة" --email admin@zu.edu.jo
  4) ابدأ الخدمات:
       sudo systemctl start zu-training-api zu-training-portal
       sudo systemctl status zu-training-api zu-training-portal
DONE
