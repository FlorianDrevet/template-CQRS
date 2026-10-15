# Health checks

Un *health check* est un endpoint qui répond à la question « cette application va-t-elle bien ? ». Cette page explique à quoi il sert, comment le template l'expose et comment en ajouter un.

## Le concept simplement

Un serveur peut être « allumé » sans être utilisable : il démarre encore, il a perdu sa base de données, il est bloqué. Un orchestrateur (Aspire en local, Kubernetes ou Azure Container Apps en production) ne peut pas le deviner : il interroge donc régulièrement un endpoint dédié et agit selon la réponse (attendre, rediriger le trafic ailleurs, redémarrer le processus).

On distingue deux questions :

| Sonde | Question | Si la réponse est « non » |
|-------|----------|---------------------------|
| **Liveness** (vivacité) | « Le processus fonctionne-t-il, ou est-il figé ? » | on le **redémarre** |
| **Readiness** (disponibilité) | « Est-il prêt à recevoir du trafic maintenant ? » (base joignable, démarrage terminé) | on **cesse de lui envoyer du trafic**, sans le redémarrer |

L'analogie est celle d'un restaurant : la liveness vérifie que le cuisinier est présent et conscient ; la readiness vérifie que la cuisine est allumée et les ingrédients livrés avant d'ouvrir la salle.

## Pourquoi c'est important

- **Démarrage ordonné** : en local, Aspire attend que l'API réponde avant de la déclarer prête.
- **Récupération automatique** : un processus bloqué est redémarré sans intervention humaine.
- **Déploiements sans coupure** : le trafic n'est dirigé vers une nouvelle version qu'une fois qu'elle est prête.
- **Un faux positif coûte cher** : une sonde de liveness qui dépend d'une base de données ferait redémarrer l'API à chaque panne de la base, ce qui aggrave l'incident. D'où la séparation entre les deux sondes.

## Comment le template le fait

!!! info "Les lignes `#if` du dépôt"
    Les health checks se trouvent dans le projet `ServiceDefaults`, qui n'existe dans un projet généré que si l'option Aspire (`EnableAspire`) est activée. Dans `Program.cs`, l'appel `app.MapDefaultEndpoints()` est entouré d'un `#if (EnableAspire)` pour la même raison.

### Enregistrer le check

```csharp title="src/Web.Template.CQRS.ServiceDefaults/Extensions.cs"
--8<-- "src/Web.Template.CQRS.ServiceDefaults/Extensions.cs" start="public static TBuilder AddDefaultHealthChecks" before="public static WebApplication MapDefaultEndpoints"
```

Un seul check existe : `self`, qui retourne toujours `Healthy`. Il signifie simplement « le processus répond ». Il porte l'étiquette (`tag`) `live`, qui sert à le sélectionner pour la sonde de vivacité.

### Exposer les endpoints

```csharp title="src/Web.Template.CQRS.ServiceDefaults/Extensions.cs"
--8<-- "src/Web.Template.CQRS.ServiceDefaults/Extensions.cs" start="if (app.Environment.IsDevelopment() ||" before="return app;"
```

Deux endpoints en découlent :

| Endpoint | Contenu | Rôle |
|----------|---------|------|
| `/health` | **tous** les checks enregistrés | disponibilité globale (readiness) : c'est ici que viendront vos checks de dépendances |
| `/alive` | uniquement les checks étiquetés `live` | vivacité (liveness) : ne dépend d'aucune ressource externe |

Les deux sont marqués `AllowAnonymous()` : un orchestrateur n'a pas de jeton, et l'autorisation globale ne doit pas les bloquer.

### Pourquoi seulement en Development et Testing

La condition `IsDevelopment() || IsEnvironment("Testing")` est volontaire. Un endpoint de santé public peut révéler à un attaquant quelles dépendances existent et dans quel état elles sont. Les rendre accessibles seulement en local et dans les tests d'intégration évite d'exposer cette information par défaut. En production, exposez-les consciemment, par exemple sur un port interne ou derrière une règle réseau, comme le recommande la [documentation Microsoft sur les health checks](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks).

### L'usage par Aspire

L'`AppHost` déclare l'API avec une vérification de santé HTTP sur `/alive` :

```csharp title="src/Web.Template.CQRS.AppHost/Program.cs"
--8<-- "src/Web.Template.CQRS.AppHost/Program.cs" start="var api = builder" before="builder.Build().Run();"
```

`WithHttpHealthCheck("/alive")` fait interroger cette route par Aspire. Le dashboard affiche alors l'état de la ressource (en bonne santé ou non), et les ressources qui `WaitFor` l'API attendent qu'elle soit saine. Les requêtes vers `/alive` et `/health` sont exclues des traces (voir [Observabilité](observabilite.md)) pour ne pas les noyer.

### Et en production

Dans un orchestrateur, vous configurez les mêmes URL comme sondes : liveness sur `/alive`, readiness sur `/health`. Ce qui reste à décider est l'exposition (voir plus haut).

## Ajouter un health check

Pour vérifier une dépendance, par exemple la base de données, ajoutez un check dans `AddDefaultHealthChecks` **sans** l'étiquette `live` : il sera visible sur `/health` mais n'affectera pas `/alive`, et l'API ne sera donc pas redémarrée à cause d'une panne de base. Avec EF Core, le paquet `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` fournit `AddDbContextCheck<TContext>()`. Vous pouvez aussi écrire votre propre check en implémentant `IHealthCheck`. Le template n'en ajoute aucun par défaut.

## À essayer

1. Lancez le projet avec Aspire et appelez `curl -k https://localhost:7246/alive` puis `curl -k https://localhost:7246/health`. Que renvoient-ils ?
2. Dans le dashboard, regardez l'état de la ressource `api`. Quelle route la fait-elle passer en bonne santé ?
3. Cherchez les appels à `/alive` dans les traces : en trouvez-vous ? Pourquoi ?
4. Question : pourquoi mettre un check de base de données sur `/health` plutôt que sur `/alive` ?

## Pour aller plus loin

- [Health checks dans ASP.NET Core (Microsoft Learn)](https://learn.microsoft.com/aspnet/core/host-and-deploy/health-checks)
