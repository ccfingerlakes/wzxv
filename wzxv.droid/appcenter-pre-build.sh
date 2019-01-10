#!/usr/bin/env bash

if [ ! -n "$APPCENTER_BUILD_ID" ]
then
    echo "The script can only be run from AppCenter, the APPCENTER_BUILD_ID variable is missing"
    exit
fi

if [ ! -n "$APPCENTER_APPSECRET" ]
then
	echo "The script can only be run from AppCenter, the APPCENTER_APPSECRET variable is missing"
fi

APPCENTERCONFIG_FILE=$APPCENTER_SOURCE_DIRECTORY/wzxv.droid/AppCenterConfig.cs
ANDROID_MANIFEST_FILE=$APPCENTER_SOURCE_DIRECTORY/wzxv.droid/Properties/AndroidManifest.xml
TODAY=$(date +"%Y%m%d")

if [ -e "$ANDROID_MANIFEST_FILE" ]
then
    echo "Replacing versionName with '$TODAY.$APPCENTER_BUILD_ID' in '$ANDROID_MANIFEST_FILE'"
    sed -i '' 's/versionName="[0-9.]*"/versionName="'$TODAY'.'$APPCENTER_BUILD_ID'"/' "$ANDROID_MANIFEST_FILE"
else
	echo "Android manifest '$ANDROID_MANIFEST_FILE' could not be found"
fi

if [ -e "$APPCENTERCONFIG_FILE" ]
then
	echo "Updating AppCenterConfig.AppSecret to $APPCENTER_APPSECRET in $APPCENTERCONFIG_FILE"
	sed -i '' 's#AppSecret = "[-A-Za-z0-9:_./]*"#AppSecret = "'$APPCENTER_APPSECRET'"#' $APPCENTERCONFIG_FILE
else
	echo "AppCenterConfig.cs could not be found at $APPCENTERCONFIG_FILE"
fi