# GPX exporter from Garmin connect links

Convert public Garmin Connect links to gpx, even without an account.

First version: Bash script, depending on `curl` for HTTP requests and `jq` for JSON parsing.
Usage: invoke the script with the URL you wish to convert, e.g.

```
bash garmin.sh https://connect.garmin.com/modern/activity/xxyyzz
```
