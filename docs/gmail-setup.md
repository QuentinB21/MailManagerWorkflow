# Connexion d’une boîte Gmail réelle

La boîte est connectée depuis React avec le flux OAuth 2.0 « application serveur Web ». Google affiche sa propre page de connexion et de consentement : Mail Manager ne reçoit jamais le mot de passe Gmail.

## 1. Préparer Google Cloud une seule fois

L’écran **Boîte Gmail** contient les liens directs, l’URI à copier et le formulaire de configuration. Aucune modification de `.env` ni reconstruction du conteneur n’est nécessaire.

1. Ouvrir [Gmail API](https://console.cloud.google.com/apis/library/gmail.googleapis.com), sélectionner ou créer un projet, puis activer l’API.
2. Configurer [l’écran de consentement](https://console.cloud.google.com/auth/branding). Pour le POC, conserver le statut **Testing**.
3. Ajouter l’adresse Gmail utilisée dans les [utilisateurs de test](https://console.cloud.google.com/auth/audience).
4. Créer un client dans [Google Auth Platform > Clients](https://console.cloud.google.com/auth/clients), de type **Application Web**.
5. Ajouter exactement cette URI dans **Authorized redirect URIs** :

   ```text
   http://localhost:8080/api/gmail/oauth/callback
   ```

Google exige une correspondance exacte de l’URI, y compris le protocole, le port et le chemin. `localhost` est autorisé en HTTP pour le développement local. Voir la [documentation OAuth serveur Web](https://developers.google.com/identity/protocols/oauth2/web-server).

Google ne permet pas de créer ou modifier les clients OAuth par une API : cette étape reste donc volontairement dans Google Cloud Console. Toutes les autres étapes sont pilotées depuis Mail Manager.

## 2. Enregistrer le client depuis l’application

1. Ouvrir [http://localhost:5173](http://localhost:5173), puis **Boîte Gmail**.
2. Coller le **Client ID** et le **Client secret** générés par Google.
3. Cliquer sur **Enregistrer la configuration**.
4. Cliquer sur **Connecter mon compte Gmail**, choisir le compte de test et accepter la permission.
5. Après le retour dans l’application, cliquer sur **Vérifier la connexion**.

Le secret est chiffré par ASP.NET Core Data Protection avant son stockage dans PostgreSQL. Il n’est jamais renvoyé par l’API. Une modification ultérieure peut conserver le secret existant en laissant le champ vide.

Les variables `GMAIL_CLIENT_ID` et `GMAIL_CLIENT_SECRET` restent acceptées comme mécanisme de compatibilité pour un déploiement administré, mais elles ne sont pas nécessaires pour l’usage local depuis l’application.

L’application demande uniquement le scope `gmail.modify`. Il permet de lire les messages nécessaires au classement, de créer les labels manquants et de les appliquer. Voir la [liste des scopes Gmail](https://developers.google.com/workspace/gmail/api/auth/scopes).

## 3. Vérifier le classement automatique

1. Créer au moins un label et une règle active dans **Configuration**.
2. Envoyer un nouvel email correspondant vers la boîte Gmail connectée.
3. Attendre au maximum une minute : n8n vérifie automatiquement les nouveaux messages, même s’ils ont déjà été ouverts entre-temps.
4. Vérifier dans Gmail que le label a été créé ou appliqué, puis consulter l’historique dans Mail Manager.

Le bouton **Vérifier maintenant** reste disponible pour les diagnostics, mais il n’est pas nécessaire au fonctionnement normal.

Un label configuré dans Mail Manager n’a pas besoin d’être créé manuellement dans Gmail. Lors de sa première utilisation, l’API cherche un label Gmail du même nom, le crée s’il est absent, puis vérifie que Gmail l’a effectivement ajouté au message. L’écran **Configuration** indique alors **Créé dans Gmail** et l’historique distingue **Label appliqué** d’un éventuel **Échec Gmail**.

Le workflow n8n ne reçoit pas le sujet ni le corps. Il transmet uniquement le `mailboxConnectionId` et la limite à l’API. L’API récupère chaque message, utilise son contenu en mémoire pour la classification, ne conserve que la décision, puis applique le label Gmail.

## Sécurité et limites du POC

- le client secret et le refresh token sont chiffrés ; les clés Data Protection vivent dans le volume Docker `api-data-protection-keys` ;
- **Déconnecter** tente de révoquer le jeton chez Google puis efface la copie locale ;
- aucun corps ou sujet complet n’est stocké en base ou écrit volontairement dans les logs ;
- l’application ne possède pas encore d’authentification : cette intégration est destinée exclusivement à une machine locale de développement ;
- Gmail Push/Pub/Sub n’est pas encore utilisé : n8n interroge la boîte chaque minute, ce qui implique un délai maximal d’environ une minute ;
- en mode OAuth **Testing**, Google peut imposer des contraintes ou expirations supplémentaires aux jetons.

## Dépannage

- `redirect_uri_mismatch` : vérifier l’URI exacte configurée dans Google Cloud ;
- bouton de connexion désactivé : enregistrer le Client ID et le secret dans l’écran **Boîte Gmail** ;
- application non vérifiée : vérifier que le compte Gmail est déclaré comme utilisateur de test ;
- changement de client OAuth : déconnecter d’abord la boîte, modifier la configuration, puis refaire le consentement.
