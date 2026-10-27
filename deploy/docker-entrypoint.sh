#!/usr/bin/env sh
# تشغيل الـAPI أو البوابة داخل نفس الصورة حسب الخدمة المطلوبة.
set -e

case "${1:-api}" in
  api)
    exec dotnet /app/api/ZU.Training.WebApi.dll "${@:2}"
    ;;
  migrate)
    exec dotnet /app/api/ZU.Training.WebApi.dll --migrate
    ;;
  bootstrap-admin)
    shift
    exec dotnet /app/api/ZU.Training.WebApi.dll --bootstrap-admin "$@"
    ;;
  portal)
    exec dotnet /app/portal/ZU.Training.WebPortal.dll
    ;;
  *)
    exec "$@"
    ;;
esac
