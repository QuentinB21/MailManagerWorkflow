FROM quay.io/keycloak/keycloak:26.7.1

COPY --chown=keycloak:keycloak infra/keycloak/import/ /opt/keycloak/data/import/
COPY --chown=keycloak:keycloak infra/keycloak/themes/ /opt/keycloak/themes/
COPY --chown=keycloak:keycloak apps/web/public/logo.svg /opt/keycloak/themes/mail-manager/login/resources/img/logo.svg
COPY --chown=keycloak:keycloak apps/web/public/favicon.ico /opt/keycloak/themes/mail-manager/login/resources/img/favicon.ico
