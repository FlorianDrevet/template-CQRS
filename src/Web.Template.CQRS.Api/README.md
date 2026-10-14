# API

L'API valide les access tokens bearer émis par Entra. Lorsque la variante Aspire est activée, le profil de développement utilise le realm Keycloak local démarré par l'AppHost, qui émet des tokens de même forme qu'Entra. Sans Aspire, le profil de développement utilise la configuration Entra de `appsettings.json`.

Configurez `Auth:Provider`, puis les paramètres correspondants :

- Entra : `Auth:Instance`, `Auth:TenantId`, `Auth:ClientId` et, si besoin, `Auth:Audience`.
- Keycloak : `Auth:Authority` et `Auth:Audience`.

Selon le modèle d'autorisation généré :

- `Authorization/AppRoles.cs` liste les valeurs des app roles Entra ;
- `Authorization:Groups:<Nom>` contient l'object ID de chaque groupe de sécurité Entra (validé au démarrage) ;
- `Authorization/Policies.cs` et `Authorization/AuthorizationSetup.cs` définissent les policies nommées, appliquées aux endpoints avec `RequireAuthorization(Policies.RequireAdmin)`.

En local, Aspire injecte la configuration Keycloak et exporte automatiquement OpenTelemetry vers son tableau de bord. Scalar permet de s'authentifier avec le client public `template-scalar` ; `scripts/get-token` fournit un token pour un persona via le client `template-cli`. Appelez l'API avec un token sur `https://localhost:7246` (HTTP redirige vers HTTPS et perd l'en-tête `Authorization`).

L'endpoint `GET /me` retourne l'identité, les rôles et les groupes du token. Les exceptions et erreurs de validation sont converties en réponses Problem Details sans exposer le détail des erreurs internes.
