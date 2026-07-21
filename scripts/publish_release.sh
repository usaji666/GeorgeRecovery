#!/bin/zsh
set -euo pipefail

repo_dir="${0:A:h:h}"
project_dir="$repo_dir/GeorgeRecovery"
manifest="$project_dir/manifest.json"
dotnet_bin="/Users/wanghan/.dotnet/dotnet"

version="$(sed -n 's/.*"Version": "\([^"]*\)".*/\1/p' "$manifest" | head -1)"
if [[ -z "$version" ]]; then
  print -u2 "无法从 manifest.json 读取版本号。"
  exit 1
fi

tag="v$version"
zip_file="$project_dir/bin/Debug/net6.0/GeorgeRecovery $version.zip"

if ! gh auth status >/dev/null 2>&1; then
  print -u2 "GitHub CLI 尚未登录，请先运行 gh auth login。"
  exit 1
fi

"$dotnet_bin" build "$project_dir/GeorgeRecovery.csproj"

if [[ ! -f "$zip_file" ]]; then
  print -u2 "没有找到构建包：$zip_file"
  exit 1
fi

if ! git -C "$repo_dir" diff --quiet || ! git -C "$repo_dir" diff --cached --quiet; then
  print -u2 "仓库仍有未提交改动，请先提交后再发布。"
  exit 1
fi

if ! git -C "$repo_dir" rev-parse "$tag" >/dev/null 2>&1; then
  git -C "$repo_dir" tag -a "$tag" -m "治疗乔治腿伤 $tag"
fi

git -C "$repo_dir" push origin main
git -C "$repo_dir" push origin "$tag"

if gh release view "$tag" --repo "$(git -C "$repo_dir" remote get-url origin)" >/dev/null 2>&1; then
  print "GitHub Release $tag 已存在。"
else
  gh release create "$tag" "$zip_file" \
    --repo "$(git -C "$repo_dir" remote get-url origin)" \
    --title "治疗乔治腿伤 $tag" \
    --generate-notes
fi

print "发布完成：$tag"
