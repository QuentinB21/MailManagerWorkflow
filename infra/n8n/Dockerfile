FROM n8nio/n8n:2.34.4

USER root
COPY --chown=node:node infra/n8n/bootstrap.sh /opt/mail-manager/bootstrap.sh
COPY --chown=node:node infra/n8n/workflows/ /opt/mail-manager/workflows/
RUN chmod 755 /opt/mail-manager/bootstrap.sh
USER node

ENTRYPOINT ["/bin/sh", "/opt/mail-manager/bootstrap.sh"]
