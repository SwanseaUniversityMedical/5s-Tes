#!/bin/sh
set -e

KIBANA_URL="http://kibana:5601"

echo "Waiting for Kibana..."
until curl -sf -o /dev/null "$KIBANA_URL/api/status"; do
  sleep 10
done
echo "Kibana is up. Importing dashboard..."

# Create the data view for zeebe-record_*
curl -sf -X POST "$KIBANA_URL/api/data_views/data_view" \
  -H "Content-Type: application/json" \
  -H "kbn-xsrf: true" \
  -d '{
    "data_view": {
      "id": "zeebe-data-view",
      "title": "zeebe-record_*",
      "name": "Zeebe Records",
      "timeFieldName": "timestamp"
    },
    "override": true
  }' && echo "Data view created" || echo "Data view already exists, skipping"

# Import saved objects (visualizations + dashboard)
curl -sf -X POST "$KIBANA_URL/api/saved_objects/_import?overwrite=true" \
  -H "kbn-xsrf: true" \
  -F "file=@/init/dashboard.ndjson" \
  && echo "Dashboard imported successfully" \
  || echo "Dashboard import failed - check Kibana logs"

echo "Done. Access Kibana at http://localhost:5601"
