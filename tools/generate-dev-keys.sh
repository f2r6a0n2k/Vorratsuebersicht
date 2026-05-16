#!/bin/bash
# Generate development keystores for local APK signing
# For production, use GitHub Secrets instead (see .github/workflows/build.yml)

set -e

KEYPASS="${1:-vorratsync}"
VALIDITY=10000

echo "=== Generating APK signing keystore ==="
keytool -genkey -v \
  -keystore ../vorratsuebersicht.keystore \
  -alias vorratsuebersicht \
  -keyalg RSA -keysize 2048 \
  -validity $VALIDITY \
  -storepass "$KEYPASS" -keypass "$KEYPASS" \
  -dname "CN=Dev, OU=Development, O=Vorratsuebersicht, L=Unknown, ST=Unknown, C=DE"

echo "=== Generating F-Droid repo signing keystore ==="
keytool -genkey -v \
  -keystore ../fdroid/fdroid-repo.keystore \
  -alias fdroid-repo \
  -keyalg RSA -keysize 2048 \
  -validity $VALIDITY \
  -storepass "$KEYPASS" -keypass "$KEYPASS" \
  -dname "CN=FDroidRepo, OU=Development, O=Vorratsuebersicht, L=Unknown, ST=Unknown, C=DE"

echo ""
echo "Done! Keystores created with password: $KEYPASS"
echo "  APK signing:    vorratsuebersicht.keystore"
echo "  F-Droid repo:   fdroid/fdroid-repo.keystore"
echo ""
echo "To sign APK locally with this keystore:"
echo "  msbuild Vorratsübersicht.csproj /t:PackageForAndroid /p:Configuration=Release \\"
echo "    /p:AndroidKeyStore=true \\"
echo "    /p:AndroidSigningKeyStore=\$(pwd)/vorratsuebersicht.keystore \\"
echo "    /p:AndroidSigningStorePass=$KEYPASS \\"
echo "    /p:AndroidSigningKeyAlias=vorratsuebersicht \\"
echo "    /p:AndroidSigningKeyPass=$KEYPASS"
