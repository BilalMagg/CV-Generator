#!/bin/sh
set -e

# Render injects $PORT (e.g. 10000); locally it defaults to 4200.
LISTEN_PORT="${PORT:-4200}"
sed -i "s/listen [0-9]\+;/listen ${LISTEN_PORT};/" /etc/nginx/sites-available/propel.conf

exec /usr/bin/supervisord -n -c /etc/supervisor/supervisord.conf
