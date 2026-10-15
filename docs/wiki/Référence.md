# Référence

Cette section rassemble les informations à consulter plutôt qu'à lire : tableaux d'options, clés de configuration, endpoints, scripts, pipelines et arborescence. Chaque page est exacte par rapport au code du dépôt (les extraits sont inclus depuis les fichiers réels) et renvoie vers la page de cours qui explique la notion.

| Page | Vous y trouvez | Utile quand |
|------|----------------|-------------|
| [Options du template](/Référence/Options-du-template.md) | les quatre options de `dotnet new`, leurs valeurs, les symboles calculés, les fichiers ajoutés ou retirés | vous choisissez comment générer le projet |
| [Configuration](/Référence/Configuration.md) | toutes les clés de configuration, leur type, leur défaut et la variable d'environnement équivalente | vous configurez un environnement ou diagnostiquez un démarrage qui échoue |
| [Endpoints](/Référence/Endpoints.md) | chaque route, son authentification, sa policy, sa limite de débit et ses réponses | vous appelez l'API ou écrivez un client |
| [Scripts et CI](/Référence/Scripts-et-CI.md) | `get-token`, `verify-template.sh`, les workflows GitHub et les pipelines Azure DevOps, le développement du template lui-même | vous automatisez, vous branchez la CI ou vous contribuez au template |
| [Arborescence](/Référence/Arborescence.md) | l'arborescence d'un projet généré, commentée, et ce qui change selon les options | vous vous repérez dans un projet généré |
| [Contribuer à la doc](/Référence/Contribuer-à-la-doc.md) | l'outillage de documentation, la syntaxe autorisée, la publication du wiki Azure DevOps | vous écrivez ou corrigez une page |

## Conventions de cette section

- Les noms de projet suivent le code du dépôt (`Web.Template.CQRS`). Dans un projet généré, ce préfixe est remplacé par le nom que vous avez choisi, par exemple `Contoso.Orders`.
- Le code du dépôt est la **source du template** : il contient des directives comme `#if (EnableAspire)`. Dans un projet généré, seules les branches correspondant à vos options sont conservées.
- Une clé hiérarchique de configuration s'écrit avec `:` dans les fichiers et avec `__` dans les variables d'environnement : `Auth:TenantId` devient `Auth__TenantId`.

Si un terme vous échappe, consultez le [glossaire](/Glossaire.md).
