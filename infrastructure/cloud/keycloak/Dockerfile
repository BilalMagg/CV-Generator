# Custom Keycloak image — bakes the realm into the import directory.
#
# WHY: docker-compose mounted the realm via a host bind-mount
# (./keycloak-realm:/opt/keycloak/data/import). ECS has no host bind-mount, so
# the realm JSON must be inside the image or `--import-realm` finds nothing and
# login config (clients, redirect URIs, IdPs) never loads. (§5.5)
#
# BUILD CONTEXT must be the repo's realm directory so the COPY can see the JSON:
#   docker build -f infrastructure/cloud/keycloak/Dockerfile \
#                -t <ecr>/cvgen/keycloak:latest \
#                api_gateway/keycloak-realm
# (The Ansible `images` role does exactly this — see roles/images.)

FROM quay.io/keycloak/keycloak:26.2

# Bake the realm export(s) into the import dir.
COPY . /opt/keycloak/data/import/

# Pre-build for production `start --optimized`. KC_DB must be known at build
# time; the actual DB host/credentials are supplied at runtime via env/secrets.
ENV KC_DB=postgres
RUN /opt/keycloak/bin/kc.sh build

ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
