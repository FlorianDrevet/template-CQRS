# Dépannage de l'environnement local

Premier réflexe : `aspire describe` (quelle ressource est en échec ?) puis `aspire logs <ressource> --tail 50`. L'API attend ses dépendances (`WaitFor`) : si `keycloak` n'est pas prêt, `api` ne démarre pas. `APPHOST` désigne `src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj`.

| Symptôme | Cause probable | Remède |
|----------|----------------|--------|
| `api` reste en attente indéfiniment, `keycloak` en échec ou absent | Le port 8080 est pris, souvent par le conteneur Keycloak persistant d'un autre projet généré | `docker ps --filter name=keycloak` ; **proposez** `aspire stop --apphost <autre csproj> --force` ou `docker stop <conteneur>`, appliquez après accord |
| Erreur de certificat avec `curl`, `get-token` ou dans les logs de l'API (téléchargement des clés) | Certificat de développement non approuvé | `dotnet dev-certs https --check --trust` ; avec accord : `dotnet dev-certs https --trust` |
| `invalid_grant` dans `get-token` | Mauvais mot de passe, persona inexistant (realm non importé ou persona absent de cette variante), action requise sur le compte | Vérifiez l'e-mail (`admin@local.test`, `user@local.test`) et le mot de passe `Passw0rd!` ; comparez avec le realm du dépôt |
| Persona ou rôle récent absent, modification du realm sans effet | Le realm n'est importé qu'au premier démarrage du volume Keycloak | Réimport = `aspire stop --apphost APPHOST --force --volumes` : **destructif** (la base SQL aussi). Demandez l'accord, ou, avec accord explicite, réinitialisez seulement Keycloak par la méthode Docker de `docs/site/aspire/emulateurs.md` si présente (elle supprime aussi des données) |
| 401 sur un token pourtant valide, issuer invalide dans les logs | L'`iss` du token ne correspond pas à `Auth:Authority` : token demandé via `127.0.0.1` ou un nom de conteneur | Demandez le token à `https://localhost:8080` (c'est ce que font les scripts) ; vérifiez `iss` en décodant le token localement |
| 401 et `aud` sans `template-api` | Token obtenu avec un autre client que `template-cli` ou `template-scalar` | Utilisez `scripts/get-token.*` ; le mapper `template-api-audience` doit exister dans le realm |
| 401 en appelant le port 5257 | Le port HTTP redirige vers HTTPS et la redirection perd `Authorization` | Appelez `https://localhost:7246` |
| 403 pour l'administrateur | Claim `roles` ou `groups` absent ou différent (modèle d'autorisation, GUID local de groupe `6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11`) | Décodez le token localement, comparez à la section « Modèle d'autorisation » d'`AGENTS.md` et au realm ; voir `manage-access` |
| 500 sur une route qui touche la base | Migration non appliquée, ou chaîne de connexion absente | `aspire logs api --tail 100`, `aspire otel logs api --severity Error` ; vérifiez qu'une migration existe et que le bloc `Database.Migrate()` (Development) est présent dans `Program.cs` |
| Migration au démarrage qui échoue ou boucle | SQL Server pas encore prêt, ou migration incohérente avec la base existante | `aspire wait projectdb`, lisez `aspire logs api` ; si la base locale est jetable, demandez avant de la réinitialiser (`--force --volumes`) |
| Ressource bloquée à « démarrage » ou « en attente » | Dépendance non saine, ou secret absent (`Parameters:sql-password`, `Parameters:keycloak-admin-password`) | `aspire describe <ressource>`, `aspire logs <ressource>` ; contrôlez la présence des clés avec `dotnet user-secrets list --project APPHOST` (sans recopier les valeurs) |
| `sqlserver` en échec au démarrage | Mot de passe SQL trop faible (politique SQL Server) | Remplacez-le par une valeur plus longue avec majuscules, minuscules, chiffres et symbole ; relancez |
| Premier démarrage très long | Téléchargement des images de conteneurs | Patientez ; `aspire wait api --timeout 300` |
| Aucun log pour une 401 ou un 200 | `Microsoft.AspNetCore` est en `Warning` par défaut | Lisez la trace : `aspire otel traces api --trace-id <traceId>` ; ou montez temporairement le niveau (SKILL.md, étape 7) |
| `aspire` introuvable ou option inconnue | CLI absente ou ancienne | `aspire --version` ; `aspire <commande> --help` pour les options réellement disponibles |
| Deux instances qui se disputent les ports | Un autre AppHost tourne encore | `aspire ps`, puis `aspire stop --apphost APPHOST` pour celui du projet courant ; l'autre : demandez |
