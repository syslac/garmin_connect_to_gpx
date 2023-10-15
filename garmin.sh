#!/bin/bash

function clean_up() 
{
	rm tmp_garmin_*.txt;
	rm gpx_*.txt;
	exit $1;
}

function gpx_header() 
{
	echo "<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<gpx creator=\"\" version=\"1.1\"
  xsi:schemaLocation=\"http://www.topografix.com/GPX/1/1 http://www.topografix.com/GPX/11.xsd\"
  xmlns=\"http://www.topografix.com/GPX/1/1\"
  xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:ns2=\"http://www.garmin.com/xmlschemas/GpxExtensions/v3\">
  <metadata>
    <name>$1</name>
  </metadata>
  <trk>" > $1;
}

function gpx_tail()
{
	echo "  </trk>
</gpx>" >> $1;
}

function read_delete_first_line()
{
	head -n 1 $1;
	sed -i '1d' $1;
}

function gpx_trkpt()
{
      echo "    <trkpt lat=\"$2\" lon=\"$3\">
      <ele>$4</ele>
    </trkpt>">> $1;
}

echo "Analyzing activity $1";
ACT_ID=`echo $1 | awk -F '/' '{ print $6 }'`;
echo "Id: $ACT_ID";

echo "First GET to set cookies";
if curl -s -o 'tmp_garmin_del.txt' -c 'tmp_garmin_cookies.txt' "$1"; then

	echo "Get public auth";
	if curl -s -o 'tmp_garmin_auth.txt' -X POST -b 'tmp_garmin_cookies.txt' 'https://connect.garmin.com/services/auth/token/public'; then
		TOKEN_ID=`jq -r '.access_token' < 'tmp_garmin_auth.txt'`;
		echo "Got token $TOKEN_ID";

		if curl --compressed -H 'User-Agent: Mozilla/5.0' -H 'Accept: application/json, text/javascript, */*; q=0.01' -H 'Accept-Language: en-US,en;q=0.5' -H 'Accept-Encoding: gzip, deflate, br' -H 'NK: NT' -H 'X-app-ver: 4.72.1.2' -H 'X-lang: it-IT' -H 'DI-Backend: connectapi.garmin.com' -H 'X-Requested-With: XMLHttpRequest' -H 'DNT: 1' -H 'Connection: keep-alive' -H "Referer: $1" -H 'Sec-Fetch-Dest: empty' -H 'Sec-Fetch-Mode: cors' -H 'Sec-Fetch-Site: same-origin' -s -o 'tmp_garmin_activity.txt' -b 'tmp_garmin_cookies.txt' -H "Authorization: Bearer $TOKEN_ID" "https://connect.garmin.com/activity-service/activity/$ACT_ID/details"; then
			LAT_INDEX=`jq ' .metricDescriptors[] | select(.key == "directLatitude").metricsIndex' < tmp_garmin_activity.txt`;
			LON_INDEX=`jq ' .metricDescriptors[] | select(.key == "directLongitude").metricsIndex' < tmp_garmin_activity.txt`;
			ALT_INDEX=`jq ' .metricDescriptors[] | select(.key == "directElevation").metricsIndex' < tmp_garmin_activity.txt`;
			TIME_INDEX=`jq ' .metricDescriptors[] | select(.key == "directTimestamp").metricsIndex' < tmp_garmin_activity.txt`;
			jq " .activityDetailMetrics[].metrics[$LAT_INDEX]" < tmp_garmin_activity.txt > gpx_latitude.txt;
			jq " .activityDetailMetrics[].metrics[$LON_INDEX]" < tmp_garmin_activity.txt > gpx_longitude.txt;
			jq " .activityDetailMetrics[].metrics[$ALT_INDEX]" < tmp_garmin_activity.txt > gpx_altitude.txt;
			jq " .activityDetailMetrics[].metrics[$TIME_INDEX]" < tmp_garmin_activity.txt > gpx_time.txt;

			gpx_header "$ACT_ID.gpx";

			NR_LINES=`jq ' .activityDetailMetrics | length' < tmp_garmin_activity.txt`;
			N_READ="0";

			while [ $N_READ -lt $NR_LINES ] 
			do
				gpx_trkpt "$ACT_ID.gpx" $(read_delete_first_line gpx_latitude.txt) $(read_delete_first_line gpx_longitude.txt) $(read_delete_first_line gpx_altitude.txt) $(read_delete_first_line gpx_time.txt);
				N_READ=$[$N_READ+1];
			done

			gpx_tail "$ACT_ID.gpx";
		else
			clean_up 1;
		fi

	else
		clean_up 1
	fi
else
	clean_up 1
fi
clean_up 0
