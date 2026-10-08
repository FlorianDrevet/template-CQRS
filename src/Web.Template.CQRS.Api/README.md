# API

L'API valide les access tokens bearer émis par Entra. Lorsque la variante Aspire est activée, le profil de développement utilise le realm Keycloak local démarré par l'AppHost. Sans Aspire, le profil de développement utilise la configuration Entra de `appsettings.json`.

Configurez `Auth:Provider`, puis les paramètres correspondants :

- Entra : `Auth:Instance`, `Auth:TenantId`, `Auth:ClientId` et, si besoin, `Auth:Audience`.
- Keycloak : `Auth:Authority` et `Auth:Audience`.

En local, Aspire injecte la configuration Keycloak et exporte automatiquement OpenTelemetry vers son tableau de bord. Scalar permet de s'authentifier avec le client public `template-scalar` du realm importé.

L'endpoint `GET /me` retourne les revendications d'identité utiles. Les exceptions et erreurs de validation sont converties en réponses Problem Details sans exposer le détail des erreurs internes.
