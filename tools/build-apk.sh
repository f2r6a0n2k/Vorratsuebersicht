#!/bin/bash
set -e

echo "=== Android SDK root ==="
echo "ANDROID_SDK_ROOT=${ANDROID_SDK_ROOT:?not set}"

echo "=== Find sdkmanager ==="
SDKMAN=$(find "$ANDROID_SDK_ROOT" -name sdkmanager -type f 2>/dev/null | head -1)
echo "sdkmanager: ${SDKMAN:?NOT FOUND}"

echo "=== Available platforms before ==="
ls "$ANDROID_SDK_ROOT/platforms/" 2>/dev/null || echo "empty"

echo "=== Install additional Android SDK platforms ==="
yes | "$SDKMAN" "platforms;android-31" "platforms;android-32" "platforms;android-33"

echo "=== Available platforms after ==="
ls "$ANDROID_SDK_ROOT/platforms/"

echo "=== Xamarin.Android framework versions ==="
ls /usr/lib/mono/xbuild/Xamarin/Android/ | grep "^v" || echo "No v-dirs found"

echo "=== Patch csproj for Linux compatibility ==="
sed -i \
  -e "s|<TargetFrameworkVersion>v13.0</TargetFrameworkVersion>|<TargetFrameworkVersion>v12.0</TargetFrameworkVersion>|" \
  -e "/<IntermediateOutputPath>/d" \
  "/workspace/Vorratsübersicht.csproj"
grep -E "TargetFrameworkVersion|IntermediateOutputPath" "/workspace/Vorratsübersicht.csproj" || true

echo "=== NuGet Restore ==="
msbuild /workspace/de.stryi.sln /restore /p:Configuration=Release

echo "=== Build & Sign APK ==="
msbuild "/workspace/Vorratsübersicht.csproj" \
  /t:SignAndroidPackage \
  /p:Configuration=Release \
  /p:AndroidKeyStore=true \
  /p:AndroidSigningKeyStore=/workspace/vorratsuebersicht.keystore \
  /p:AndroidSigningStorePass=vorratsync \
  /p:AndroidSigningKeyAlias=vorratsuebersicht \
  /p:AndroidSigningKeyPass=vorratsync \
  /p:OutputPath=/workspace/bin/

echo "=== Build output ==="
find /workspace -name "*.apk" -type f 2>/dev/null
ls -la /workspace/bin/ 2>/dev/null || echo "bin/ empty"
