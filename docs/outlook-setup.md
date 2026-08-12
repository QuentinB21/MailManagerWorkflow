# Configuration Outlook / Microsoft 365

Mail Manager utilise Microsoft Graph avec un flux OAuth 2.0 serveur. L’utilisateur final se connecte ensuite depuis l’application en quelques clics ; les identifiants techniques restent configurés une seule fois par l’administrateur.

## Configuration Microsoft Entra ID

1. Ouvrir le [centre d’administration Microsoft Entra](https://entra.microsoft.com/#view/Microsoft_AAD_RegisteredApps/ApplicationsListBlade).
2. Créer une **Nouvelle inscription**.
3. Accepter les annuaires organisationnels et les comptes Microsoft personnels pour couvrir Outlook.com et Microsoft 365.
4. Ajouter une plateforme **Web** avec l’URI locale exacte `http://localhost:8080/api/outlook/oauth/callback`.
5. Ajouter les permissions Microsoft Graph **déléguées** `User.Read`, `Mail.ReadWrite` et `MailboxSettings.ReadWrite`.
6. Créer un secret client et copier immédiatement sa **valeur**.
7. Copier l’**Application (client) ID**.

## Configuration locale

Dans le fichier `.env` non versionné :

```env
OUTLOOK_CLIENT_ID=application-client-id
OUTLOOK_CLIENT_SECRET=secret-value
OUTLOOK_TENANT=common
```

Reconstruire avec `docker compose up --build -d api web n8n`, puis ouvrir **Paramètres**, cliquer sur **+ Outlook** et **Connecter ce compte Outlook**.

## Fonctionnement et sécurité

- Microsoft Graph fournit les nouveaux emails de la boîte de réception.
- Le moteur commun utilise uniquement les règles du `MailboxConnectionId` concerné.
- La destination devient une catégorie Outlook, créée automatiquement si nécessaire.
- Le corps complet n’est pas conservé.
- Le refresh token est chiffré avec ASP.NET Core Data Protection et sa rotation Microsoft est prise en charge.

Sur le VPS, remplacez l’URI locale par l’URI HTTPS publique dans Microsoft Entra et `Outlook__RedirectUri`.
