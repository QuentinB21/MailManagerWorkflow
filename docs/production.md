# Déploiement de production

Mail Manager Workflow reste une stack Docker Compose autonome. Le Caddy du
portfolio est son unique point d’entrée public et transmet les requêtes via le
réseau Docker externe `public-proxy`.

## Routes publiques

| Route | Service interne |
|---|---|
| `/projets/MailManager/` | `mail-manager-web:80` |
| `/projets/MailManager/api/*` | `mail-manager-api:8080` |
| `/projets/MailManager/health` | `mail-manager-api:8080` |
| `/projets/MailManager/auth/*` | `mail-manager-keycloak:8080` |
| `/projets/MailManager/webhook/*` | `mail-manager-n8n:5678` |

Le préfixe `/projets/MailManager` est retiré par Caddy avant la
transmission. Il ne faut donc pas configurer `UsePathBase` dans l’API.

## Premier déploiement sur le VPS

Le dossier et le dépôt doivent exister avant la première exécution de la
pipeline :

```bash
sudo mkdir -p /opt/mail-manager
sudo chown "$USER:$USER" /opt/mail-manager
git clone https://github.com/QuentinB21/MailManagerWorkflow.git /opt/mail-manager
cd /opt/mail-manager
cp .env.prod.example .env.prod
chmod 600 .env.prod
docker network inspect public-proxy >/dev/null 2>&1 || docker network create public-proxy
```

### Corriger l’ancien dossier imbriqué

Si le dépôt existe déjà sous
`/opt/mail-manager/MailManagerWorkflow`, vérifier d’abord que le dossier parent
ne contient rien d’autre que ce sous-dossier :

```bash
find /opt/mail-manager -mindepth 1 -maxdepth 1 -printf '%f\n'
```

Lorsque la sortie contient uniquement `MailManagerWorkflow`, remonter le dépôt
complet, y compris son dossier `.git`, puis contrôler son état :

```bash
cd /opt/mail-manager
find MailManagerWorkflow -mindepth 1 -maxdepth 1 -exec mv -t . -- {} +
rmdir MailManagerWorkflow
git status --short --branch
```

Le chemin final doit alors contenir directement `README.md`, `apps`, `infra`,
`tests` et `.git` sous `/opt/mail-manager`.

Renseigner ensuite toutes les valeurs de `.env.prod`. Les mots de passe, clés
n8n et secrets OAuth doivent être longs, aléatoires, uniques et ne jamais être
commités. Les champs `LEGAL_*` publics doivent être complétés avant la mise en
ligne. Aucune valeur de secours n’est appliquée par Compose : une variable
absente ou vide interrompt immédiatement la validation avec son nom exact.

Les URI de retour à déclarer chez les fournisseurs OAuth sont exactement :

- Google : `https://quentin-bouchot.fr/projets/MailManager/api/gmail/oauth/callback`
- Microsoft Entra : `https://quentin-bouchot.fr/projets/MailManager/api/outlook/oauth/callback`

Lancer le premier déploiement :

```bash
docker compose --env-file .env.prod -f docker-compose.prod.yml up -d --build --remove-orphans
```

## Realm Keycloak existant

L’import JSON ne modifie pas un realm déjà présent dans PostgreSQL. Si le realm
`mail-manager` a déjà été importé, mettre à jour le client
`mail-manager-web` dans la console d’administration :

- Valid redirect URIs : `https://quentin-bouchot.fr/projets/MailManager/*`
- Web origins : `https://quentin-bouchot.fr`
- Post logout redirect URIs : `https://quentin-bouchot.fr/projets/MailManager/*`

Les valeurs localhost peuvent être conservées si l’environnement local reste
utilisé.

## Déploiements suivants avec GitHub Actions

Le workflow `.github/workflows/deploy-production.yml` s’exécute sur chaque push vers
`master`, après les tests .NET et le build du frontend. Créer l’environnement
GitHub `production` et y définir :

- `SSH_HOST`
- `SSH_PORT`
- `SSH_USER`
- `SSH_PRIVATE_KEY`
- `DEPLOY_PATH` avec la valeur `/opt/mail-manager`

Les secrets applicatifs restent uniquement dans
`/opt/mail-manager/.env.prod` sur le VPS.

## Vérifications

```bash
cd /opt/mail-manager
docker compose --env-file .env.prod -f docker-compose.prod.yml ps
docker network inspect public-proxy
curl --fail https://quentin-bouchot.fr/projets/MailManager/health
```

Vérifier également que :

- aucun service de cette stack ne publie de port sur l’hôte ;
- `public-proxy` contient Caddy et les quatre aliases `mail-manager-*` ;
- le frontend, ses assets et un rafraîchissement direct fonctionnent ;
- Keycloak ne redirige jamais vers localhost ;
- connexion, inscription, profil démo et déconnexion reviennent sous le bon préfixe ;
- les callbacks Gmail et Outlook reviennent dans l’application ;
- les webhooks fonctionnent, tandis que l’éditeur n8n reste inaccessible publiquement.

La carte du portfolio ne doit passer à `available: true` qu’après ces contrôles.
