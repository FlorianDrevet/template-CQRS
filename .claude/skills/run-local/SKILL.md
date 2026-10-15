---
name: run-local
description: "Lancer, vérifier et dépanner l'environnement local Aspire (SQL Server, Azurite, Keycloak, API) : démarrer/arrêter, obtenir un token pour un persona, appeler l'API, lancer le smoke test, lire logs et traces via le MCP Aspire ou la CLI. À utiliser pour tester une feature en local, reproduire un bug ou diagnostiquer une erreur 401/403/500."
---

# Lancer et vérifier en local

Environnement : AppHost Aspire (`sqlserver`/`projectdb`, `storage`/`blobs`, `keycloak`, `api`). API : `https://localhost:7246` (jamais le port HTTP 5257 : la redirection perd l'en-tête `Authorization`). Keycloak : `https://localhost:8080`, realm `template`. Personas : `admin@local.test`, `user@local.test` (mot de passe `Passw0rd!`, local uniquement). Symptômes et remèdes : [references/troubleshooting.md](references/troubleshooting.md).

Dans les commandes, `APPHOST` désigne `src/Web.Template.CQRS.AppHost/Web.Template.CQRS.AppHost.csproj`.

## Étapes

1. **Prérequis.**
   - Docker tourne : `docker info` (sinon, dites-le à l'utilisateur).
   - CLI Aspire : `aspire --version`.
   - Certificat de développement : `dotnet dev-certs https --check --trust` ; s'il n'est pas approuvé, demandez avant de lancer `dotnet dev-certs https --trust` (boîte de dialogue système).
   - Secrets de l'AppHost : `dotnet user-secrets list --project APPHOST` affiche les valeurs, donc ne retenez que la présence des clés `Parameters:sql-password` et `Parameters:keycloak-admin-password` et ne répétez jamais leurs valeurs. Si une clé manque, **demandez d'abord l'accord** de l'utilisateur (c'est une écriture dans son profil), puis posez-la avec une valeur aléatoire acceptée par SQL Server (majuscules, minuscules, chiffres, symbole), sans l'afficher :
     ```bash
     dotnet user-secrets set "Parameters:sql-password" "Aa1!$(openssl rand -hex 16)" --project APPHOST
     dotnet user-secrets set "Parameters:keycloak-admin-password" "Aa1!$(openssl rand -hex 16)" --project APPHOST
     ```
     PowerShell : `dotnet user-secrets set "Parameters:sql-password" ("Aa1!" + [Convert]::ToHexString([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(16))) --project APPHOST` (idem pour `Parameters:keycloak-admin-password`).
     Ces mots de passe restent dans le profil de l'utilisateur ; ne les écrivez dans aucun fichier du dépôt.
2. **Port 8080 libre.** `docker ps --filter name=keycloak`. Si un Keycloak d'un **autre** projet occupe le port (conteneur persistant qui survit à `aspire stop`), expliquez-le à l'utilisateur et proposez `aspire stop --apphost <autre csproj> --force` ou `docker stop <conteneur>` ; n'appliquez aucune des deux d'office. Arrêter le conteneur ne suffit pas toujours : le proxy du `dcp` d'un **autre AppHost encore en marche** garde les ports 8080 et 7246 (`netstat -ano | grep -E ":(8080|7246) "` puis `aspire ps` pour repérer l'AppHost). Dans ce cas, seul `aspire stop --apphost <autre csproj>` libère les ports : proposez-le, n'appliquez rien d'office. Un seul projet généré à la fois peut tourner sur ces ports fixes.
3. **Démarrer** (en arrière-plan, rend la main) :
   ```bash
   aspire start --apphost APPHOST
   ```
   Le résultat donne l'URL du dashboard (elle contient un jeton : ne la republiez pas). Attendez ensuite que l'API soit saine, puis contrôlez :
   ```bash
   aspire wait keycloak --timeout 240
   aspire wait api --timeout 300
   aspire ps
   aspire describe
   ```
   `aspire wait <ressource>` attend l'état `healthy` par défaut (`--status`, `--timeout` en secondes, 120 par défaut). Si une ressource reste bloquée ou en échec, lisez `aspire logs <ressource> --tail 50` puis le tableau de dépannage. Premier démarrage : téléchargement des images, comptez quelques minutes. `aspire start --isolated` lance une instance parallèle avec ports aléatoires, mais Keycloak n'est alors plus sur 8080 ni l'API sur 7246 : relevez les adresses dans `aspire describe`, et n'utilisez pas les scripts sans `KEYCLOAK_URL`/`API_BASE_URL`.
4. **Token** pour un persona (`admin` ou `user`, ou l'e-mail complet) :
   ```bash
   token=$(bash ./scripts/get-token.sh admin)        # PowerShell : $token = ./scripts/get-token.ps1 -Persona admin
   ```
   Ne collez jamais un token, même local, dans un fichier ou un site externe.
5. **Appeler l'API.**
   ```bash
   curl -sS -H "Authorization: Bearer $token" https://localhost:7246/me
   curl -sS -o /dev/null -w '%{http_code}\n' https://localhost:7246/me        # sans token : 401
   ```
   PowerShell : `Invoke-RestMethod -Uri https://localhost:7246/me -Headers @{ Authorization = "Bearer $token" }` (ajoutez `-SkipHttpErrorCheck -StatusCodeVariable code` pour lire un 4xx/5xx). Une réponse d'erreur est un Problem Details : relevez `status`, `code` et `traceId`. Documentation interactive : `https://localhost:7246/scalar`.
6. **Smoke test** (401 sans token, 200 pour les deux personas sur `/me`, 200/403/401 sur `/admin/ping` quand la route existe) :
   ```bash
   bash ./scripts/smoke-test.sh        # PowerShell : ./scripts/smoke-test.ps1
   ```
   Un code de sortie non nul signifie au moins un `FAIL` : lisez ces lignes et passez à l'étape 7.
7. **Observabilité.**
   - Si les outils MCP Aspire sont disponibles (serveur `aspire`, lancé par `aspire agent mcp`), utilisez-les : état des ressources, logs console et structurés, traces.
   - Sinon, la CLI :
     ```bash
     aspire logs api --tail 100                         # sortie console d'une ressource
     aspire otel logs api --severity Error              # logs structurés
     aspire otel traces api --has-error                 # traces en échec
     aspire otel traces api --trace-id <traceId>        # une trace précise
     aspire otel logs api --trace-id <traceId>          # les logs corrélés
     ```
     Options communes : `--format Json`, `--limit`/`-n`, `--search`.
   - Une 401 ou une 403 n'est **pas** une erreur côté trace (état `OK`, `hasError` faux) : `--has-error` ne la renvoie pas. Prenez le `traceId` du Problem Details et lisez `aspire otel traces api --trace-id <traceId> --format Json` : l'attribut `http.response.status_code` donne le code.
   - Reliez toujours une erreur à sa trace : le `traceId` du Problem Details est l'identifiant à passer à `--trace-id`.
   - Niveaux de log par défaut : `Microsoft.AspNetCore` en `Warning`. Une 401 ou un 200 n'écrit donc aucun log : lisez la trace (spans). Seule une 500 écrit un `LogError` (gestionnaire d'erreurs). Pour des logs corrélés sur toutes les requêtes, relancez l'API avec `Logging__LogLevel__Microsoft.AspNetCore=Information` (avec Aspire, ajoutez temporairement `.WithEnvironment("Logging__LogLevel__Microsoft.AspNetCore", "Information")` à la ressource `api` de l'AppHost, annoncez-le et retirez-le ensuite).
8. **Arrêter.**
   ```bash
   aspire stop --apphost APPHOST
   ```
   SQL Server et Keycloak sont des conteneurs persistants avec volume : ils restent actifs avec leurs données (et gardent le port 8080). `--force` supprime les ressources persistantes (conteneurs) en gardant les volumes ; `--force --volumes` efface aussi les volumes, donc **la base SQL locale et le realm** : accord explicite requis, à chaque fois.

## Terminé quand

- `aspire describe` montre toutes les ressources saines et `scripts/smoke-test.*` finit sans `FAIL` (ou la cause de l'échec est identifiée et expliquée).
- Le comportement testé (code HTTP, corps, persona) est constaté, pas supposé.
- Pour un bug : la trace ou le log qui prouve la cause est cité (`traceId`, ressource, ligne).
- L'état final de l'environnement (laissé en marche ou arrêté) est annoncé à l'utilisateur.

## Ne jamais faire sans demander

- `aspire stop ... --force --volumes` ou toute suppression de volume ou de conteneur : efface la base et le realm.
- Arrêter ou supprimer le conteneur Keycloak (ou toute ressource) d'un autre projet.
- Modifier le realm Keycloak : il faut ensuite le réimporter (voir `manage-access`).
- Écrire des user-secrets manquants (`dotnet user-secrets set`) : accord préalable, valeurs jamais affichées.
- Approuver le certificat de développement (`dotnet dev-certs https --trust`).
- Afficher, journaliser ou committer un secret ou un token.
- Envoyer les identifiants des personas ailleurs que vers l'API et Keycloak locaux.
