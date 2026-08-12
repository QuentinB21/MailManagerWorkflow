# Connexion d’une boîte Gmail réelle

La boîte est connectée depuis React avec le flux OAuth 2.0 « application serveur Web ». Google affiche sa propre page de connexion et de consentement : Mail Manager ne reçoit jamais le mot de passe Gmail.

## 1. Préparer Google Cloud une seule fois

Cette configuration est réalisée une seule fois par l’exploitant de Mail Manager. Les utilisateurs ne saisissent jamais de Client ID ou de secret dans l’application.

1. Ouvrir [Gmail API](https://console.cloud.google.com/apis/library/gmail.googleapis.com), sélectionner ou créer un projet, puis activer l’API.
2. Configurer [l’écran de consentement](https://console.cloud.google.com/auth/branding). Pour le POC, conserver le statut **Testing**.
3. Ajouter l’adresse Gmail utilisée dans les [utilisateurs de test](https://console.cloud.google.com/auth/audience).
4. Créer un client dans [Google Auth Platform > Clients](https://console.cloud.google.com/auth/clients), de type **Application Web**.
5. Ajouter exactement cette URI dans **Authorized redirect URIs** :

   ```text
   http://localhost:8080/api/gmail/oauth/callback
   ```

Google exige une correspondance exacte de l’URI, y compris le protocole, le port et le chemin. `localhost` est autorisé en HTTP pour le développement local. Voir la [documentation OAuth serveur Web](https://developers.google.com/identity/protocols/oauth2/web-server).

Google ne permet pas de créer ou modifier les clients OAuth par une API : cette étape reste donc dans Google Cloud Console.

## 2. Installer les identifiants sur le serveur

1. Copier `.env.example` vers `.env`.
2. Renseigner `GMAIL_CLIENT_ID` et `GMAIL_CLIENT_SECRET` avec les valeurs générées par Google.
3. Reconstruire ou recréer le conteneur API avec `docker compose up --build -d api`.
4. Ouvrir [http://localhost:5173](http://localhost:5173), puis **Paramètres**.
5. Cliquer sur **Connecter mon compte Gmail**, choisir le compte et accepter la permission.
6. Après le retour dans l’application, cliquer sur **Vérifier la connexion**.

Le secret reste dans l’environnement du serveur. Il n’est ni renvoyé par l’API ni exposé dans React. Les installations configurées avec une ancienne version conservent temporairement la lecture de leur configuration chiffrée en base afin de faciliter la migration vers les variables serveur.

L’application demande uniquement le scope `gmail.modify`. Il permet de lire les messages nécessaires au classement, de créer les labels manquants et de les appliquer. Voir la [liste des scopes Gmail](https://developers.google.com/workspace/gmail/api/auth/scopes).

## 3. Vérifier le classement automatique

1. Créer au moins une destination et une règle active dans **Classement**.
2. Envoyer un nouvel email correspondant vers la boîte Gmail connectée.
3. Attendre au maximum une minute : n8n vérifie automatiquement les nouveaux messages, même s’ils ont déjà été ouverts entre-temps.
4. Vérifier dans Gmail que le label a été créé ou appliqué, puis consulter l’historique dans Mail Manager.

Le bouton **Vérifier maintenant** reste disponible pour les diagnostics, mais il n’est pas nécessaire au fonctionnement normal.

Un libellé configuré dans Mail Manager n’a pas besoin d’être créé manuellement dans Gmail. Lors de sa première utilisation, l’API cherche un libellé Gmail du même nom, le crée s’il est absent, puis vérifie que Gmail l’a effectivement ajouté au message. L’écran **Classement > Destinations** indique alors **Créé dans Gmail** et l’activité distingue **Label appliqué** d’un éventuel **Échec Gmail**.

Le workflow n8n ne reçoit pas le sujet ni le corps. Il transmet uniquement le `mailboxConnectionId` et la limite à l’API. L’API récupère chaque message, utilise son contenu en mémoire pour la classification, ne conserve que la décision, puis applique le label Gmail.

## Sécurité et limites du POC

- le client secret reste dans l’environnement du serveur et le refresh token est chiffré ; les clés Data Protection vivent dans le volume Docker `api-data-protection-keys` ;
- **Déconnecter** tente de révoquer le jeton chez Google puis efface la copie locale ;
- aucun corps ou sujet complet n’est stocké en base ou écrit volontairement dans les logs ;
- l’application ne possède pas encore d’authentification : cette intégration est destinée exclusivement à une machine locale de développement ;
- Gmail Push/Pub/Sub n’est pas encore utilisé : n8n interroge la boîte chaque minute, ce qui implique un délai maximal d’environ une minute ;
- en mode OAuth **Testing**, Google peut imposer des contraintes ou expirations supplémentaires aux jetons.

## Dépannage

- `redirect_uri_mismatch` : vérifier l’URI exacte configurée dans Google Cloud ;
- bouton de connexion désactivé : vérifier `GMAIL_CLIENT_ID` et `GMAIL_CLIENT_SECRET`, puis recréer le conteneur API ;
- application non vérifiée : vérifier que le compte Gmail est déclaré comme utilisateur de test ;
- changement de client OAuth : déconnecter d’abord la boîte, modifier les secrets du serveur, recréer l’API puis refaire le consentement.
