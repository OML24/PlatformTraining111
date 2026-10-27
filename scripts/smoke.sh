#!/usr/bin/env bash
# فحص تشغيل حقيقي: قاعدة بيانات فعلية، ترحيلات، حساب أدمن، ثم نداءات HTTP على الـAPI والبوابة.
# يُستخدم في CI وللتحقق المحلي:  bash scripts/smoke.sh
set -uo pipefail

API_URL=${API_URL:-http://127.0.0.1:5189}
PORTAL_URL=${PORTAL_URL:-http://127.0.0.1:5249}
LOGIN=${ADMIN_LOGIN:-1001}
ADMIN_PASSWORD=${ADMIN_PASSWORD:-Smoke-Test-Pass-2026!}
ARTIFACTS=${ARTIFACTS:-/tmp/smoke}
COOKIES="$ARTIFACTS/cookies.txt"
mkdir -p "$ARTIFACTS"

FAILED=0
REPORT="$ARTIFACTS/report.txt"
: > "$REPORT"
pass() { printf '  ✓ %s\n' "$1" | tee -a "$REPORT"; }
fail() { printf '  ✗ %s\n' "$1" | tee -a "$REPORT"; FAILED=$((FAILED + 1)); }

expect_code() { # الوصف، الكود المتوقع، args curl...
  local label=$1 expected=$2
  shift 2
  local body="$ARTIFACTS/last.json" code
  code=$(curl -sS -o "$body" -w '%{http_code}' "$@") || code=000
  if [[ "$code" == "$expected" ]]; then
    pass "$label ($code)"
  else
    fail "$label — متوقع $expected، Actual $code"
    head -c 400 "$body" 2>/dev/null; echo
  fi
  LAST_BODY=$(cat "$body" 2>/dev/null || true)
}

echo "==> انتظار الـAPI"
for _ in $(seq 1 60); do
  if curl -fsS "$API_URL/api/health" >/dev/null 2>&1; then break; fi
  sleep 2
done

echo "==> فحوص الـAPI"
expect_code "فحص صحة الـAPI" 200 "$API_URL/api/health"
expect_code "رفض الوصول دون جلسة" 401 "$API_URL/api/auth/me"

echo "==> تسجيل الدخول"
expect_code "دخول الأدمن" 200 -c "$COOKIES" -H 'Content-Type: application/json' \
  -d "{\"loginName\":\"$LOGIN\",\"password\":\"$ADMIN_PASSWORD\"}" "$API_URL/api/auth/login"
expect_code "جلسة نشطة" 200 -b "$COOKIES" "$API_URL/api/auth/me"
ME=$LAST_BODY
if echo "$ME" | grep -q '"mustChangePassword":true'; then
  pass "إلزام تغيير كلمة المرور مطبّق"
fi

expect_code "إحصاءات عامة" 200 -b "$COOKIES" "$API_URL/api/statistics/overview"
expect_code "قائمة الدورات" 200 -b "$COOKIES" "$API_URL/api/courses"
expect_code "دليل الأعضاء" 200 -b "$COOKIES" "$API_URL/api/members"
expect_code "الإشعارات" 200 -b "$COOKIES" "$API_URL/api/notifications"
expect_code "الصلاحيات" 200 -b "$COOKIES" "$API_URL/api/admin/roles/ADMIN/permissions"
expect_code "سجل التدقيق" 200 -b "$COOKIES" "$API_URL/api/audit"
expect_code "التحقق من شهادة غير موجودة" 404 "$API_URL/api/certificates/verify?cert=ZZ-NOT-EXIST"

echo "==> حماية المسارات الإدارية"
expect_code "منع غير المسجّل من إدارة المستخدمين" 401 "$API_URL/api/admin/users"

echo "==> البوابة"
for _ in $(seq 1 30); do
  if curl -fsS "$PORTAL_URL/health" >/dev/null 2>&1; then break; fi
  sleep 2
done
expect_code "صحة البوابة" 200 "$PORTAL_URL/health"
expect_code "صفحة الدخول" 200 "$PORTAL_URL/index.html"
expect_code "تمرير /api عبر البوابة" 200 "$PORTAL_URL/api/health"
expect_code "صفحة التحقق" 200 "$PORTAL_URL/verify.html"

if [[ $FAILED -gt 0 ]]; then
  echo "==> فشل $FAILED فحصاً"
  exit 1
fi
echo "==> كل الفحوص ناجحة"
