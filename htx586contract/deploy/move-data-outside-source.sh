#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 || $# -gt 4 ]]; then
  echo "Cách dùng: sudo $0 <thu_muc_data_cu> [thu_muc_data_moi] [service_user] [service_group]"
  echo "Ví dụ: sudo $0 /var/www/htx586contract/HTX586CONTRACT_Data /var/www/htx586contract_data www-data www-data"
  exit 1
fi

OLD_ROOT="$(readlink -f "$1")"
NEW_ROOT="${2:-/var/www/htx586contract_data}"
SERVICE_USER="${3:-root}"
SERVICE_GROUP="${4:-$SERVICE_USER}"

if [[ ! -d "$OLD_ROOT" ]]; then
  echo "Không tìm thấy thư mục dữ liệu cũ: $OLD_ROOT"
  exit 1
fi

mkdir -p "$NEW_ROOT"
NEW_ROOT="$(readlink -f "$NEW_ROOT")"

if [[ "$OLD_ROOT" == "$NEW_ROOT" ]]; then
  echo "Thư mục cũ và mới đang trùng nhau. Không thực hiện."
  exit 1
fi

mkdir -p "$NEW_ROOT/upload" "$NEW_ROOT/dataprotection-keys"

# Copy các dữ liệu khác nhưng tách riêng thư mục file upload và key để đổi đúng tên.
rsync -aHAX \
  --exclude='/uploads/' \
  --exclude='/upload/' \
  --exclude='/dataprotection-keys/' \
  "$OLD_ROOT"/ "$NEW_ROOT"/

if [[ -d "$OLD_ROOT/uploads" ]]; then
  rsync -aHAX "$OLD_ROOT/uploads"/ "$NEW_ROOT/upload"/
elif [[ -d "$OLD_ROOT/upload" ]]; then
  rsync -aHAX "$OLD_ROOT/upload"/ "$NEW_ROOT/upload"/
fi

if [[ -d "$OLD_ROOT/dataprotection-keys" ]]; then
  rsync -aHAX "$OLD_ROOT/dataprotection-keys"/ "$NEW_ROOT/dataprotection-keys"/
fi

chown -R "$SERVICE_USER:$SERVICE_GROUP" "$NEW_ROOT"
chmod 750 "$NEW_ROOT"
find "$NEW_ROOT" -type d -exec chmod 750 {} +
find "$NEW_ROOT" -type f -exec chmod 640 {} +

echo
printf 'Đã copy dữ liệu tới: %s\n' "$NEW_ROOT"
printf 'Cấu trúc vật lý: %s/upload và %s/dataprotection-keys\n' "$NEW_ROOT" "$NEW_ROOT"
printf 'Chủ sở hữu: %s:%s\n' "$SERVICE_USER" "$SERVICE_GROUP"
echo "URL public vẫn là /uploads/... để không phải sửa dữ liệu cũ trong database."
echo "Chưa xóa thư mục cũ. Hãy kiểm tra chữ ký/PDF rồi mới xóa hoặc đổi tên bản cũ."
