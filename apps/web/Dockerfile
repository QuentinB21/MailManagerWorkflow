FROM node:24-alpine AS build
WORKDIR /app
RUN npm install --global pnpm@11.16.0
COPY package.json pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY . .
ARG VITE_API_URL=http://localhost:8080
ARG VITE_N8N_WEBHOOK_URL=http://localhost:5678/webhook/mail-manager/email
ARG VITE_N8N_GMAIL_WEBHOOK_URL=http://localhost:5678/webhook/mail-manager/gmail/sync
ARG VITE_KEYCLOAK_URL=http://localhost:8081
ARG VITE_KEYCLOAK_REALM=mail-manager
ARG VITE_KEYCLOAK_CLIENT_ID=mail-manager-web
ENV VITE_API_URL=$VITE_API_URL
ENV VITE_N8N_WEBHOOK_URL=$VITE_N8N_WEBHOOK_URL
ENV VITE_N8N_GMAIL_WEBHOOK_URL=$VITE_N8N_GMAIL_WEBHOOK_URL
ENV VITE_KEYCLOAK_URL=$VITE_KEYCLOAK_URL
ENV VITE_KEYCLOAK_REALM=$VITE_KEYCLOAK_REALM
ENV VITE_KEYCLOAK_CLIENT_ID=$VITE_KEYCLOAK_CLIENT_ID
RUN pnpm build

FROM nginx:1.29-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
