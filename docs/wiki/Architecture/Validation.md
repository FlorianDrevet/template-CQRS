# Validation

Cette page explique comment le template refuse automatiquement les requêtes invalides avant qu'elles n'atteignent votre code métier.

## Le concept simplement

**Valider**, c'est vérifier qu'une donnée respecte des règles avant de s'en servir : un nom non vide, un montant positif, une adresse e-mail bien formée. Le template utilise **FluentValidation**, une bibliothèque où l'on décrit ces règles dans une classe dédiée, le **validateur**, avec une syntaxe lisible (`RuleFor(...).NotEmpty()`).

Le template ne vous demande pas d'appeler les validateurs à la main. Il les branche dans le pipeline de Mediator (voir [CQRS et Mediator](/Architecture/CQRS-et-Mediator.md)) sous la forme d'un **pipeline behavior** : un maillon qui s'exécute avant chaque handler, pour chaque commande ou query.

## Pourquoi c'est important

- **Le handler peut faire confiance à ses entrées** : s'il s'exécute, les règles de validation sont déjà respectées.
- **Aucun oubli possible** : la validation ne dépend pas de la discipline de chaque développeur, elle est appliquée à toutes les opérations.
- **Une seule forme de refus** : toutes les erreurs de validation produisent la même réponse HTTP 400, que les clients savent interpréter (voir [Erreurs et Problem Details](/Architecture/Erreurs-et-Problem-Details.md)).
- **Les règles se testent seules** : un validateur est une classe sans dépendance, simple à couvrir par des tests unitaires.

## Comment le template le fait

### Le pipeline behavior

`ValidationBehavior` reçoit tous les validateurs enregistrés pour le type de message traité. S'il n'y en a aucun, la liste est vide et la requête continue. Sinon, il les exécute tous, rassemble les échecs, et lève une `ValidationException` s'il y en a au moins un. Dans le cas contraire, il appelle `next` : le maillon suivant, c'est-à-dire en fin de chaîne le handler.

**Fichier :** `src/Web.Template.CQRS.Application/Common/Behaviors/ValidationBehavior.cs`

```csharp
public sealed class ValidationBehavior<TMessage, TResponse>(
    IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(message, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(message, cancellationToken);
    }
}
```

Deux points à retenir :

- Tous les validateurs s'exécutent avant le refus : le client reçoit **toutes** les erreurs d'un coup, pas seulement la première.
- Lever une exception interrompt le traitement : le handler n'est jamais appelé. C'est une propriété testée par `Handle_WithInvalidMessage_ThrowsValidationExceptionWithoutCallingNext`.

### L'enregistrement

Le comportement et les validateurs sont branchés dans `AddApplication`, vu dans la page [CQRS et Mediator](/Architecture/CQRS-et-Mediator.md) : `PipelineBehaviors = [typeof(ValidationBehavior<,>)]` et `AddValidatorsFromAssembly(...)`. Cette seconde ligne découvre **tous** les validateurs du projet `Application` : il suffit d'écrire une classe qui hérite de `AbstractValidator<T>`, sans rien enregistrer à la main.

### Un exemple de validateur

Voici le validateur utilisé par les tests du template. Il hérite de `AbstractValidator<SampleCommand>`, où `SampleCommand` est une commande d'exemple avec une propriété `Name`. Le constructeur déclare la règle : le nom ne doit pas être vide.

**Fichier :** `tests/Web.Template.CQRS.Application.UnitTests/Common/ValidationBehaviorTests.cs`

```csharp
public SampleCommandValidator()
{
    RuleFor(command => command.Name).NotEmpty();
}
```

Dans votre code, vous placeriez un validateur à côté de la commande qu'il protège, dans le projet `Application` (par exemple `CreateOrderCommandValidator` à côté de `CreateOrderCommand`). Il serait alors découvert automatiquement.

### Ce que reçoit le client

Une `ValidationException` n'est pas rattrapée par le code métier. Elle remonte jusqu'au gestionnaire d'erreurs global, qui la transforme en réponse **HTTP 400** au format Problem Details, avec le code `VALIDATION` et la liste des erreurs par propriété :

```json
{
  "status": 400,
  "title": "One or more validation errors occurred.",
  "code": "VALIDATION",
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736",
  "errors": {
    "Name": ["'Name' must not be empty."]
  }
}
```

Le message exact (`'Name' must not be empty.`) est le texte par défaut de FluentValidation, qui peut varier avec la langue de l'application. Le format complet est décrit dans [Erreurs et Problem Details](/Architecture/Erreurs-et-Problem-Details.md).

> 📝 **Ce que le template ne valide pas pour vous**
>
> Le comportement valide les commandes et les queries passées à Mediator. Il ne remplace pas l'autorisation (qui décide si l'appelant a le droit d'agir) ni les contraintes de la base de données. Voir la section [Identité](/Identité.md) pour l'autorisation.

## À essayer

1. Lisez les deux tests de `ValidationBehaviorTests` : quel test prouve que le handler n'est pas appelé quand le message est invalide ?
2. Exécutez-les : `dotnet test tests/Web.Template.CQRS.Application.UnitTests`.
3. Imaginez une commande `CreateOrderCommand(string CustomerName, decimal Amount)`. Écrivez sur papier son validateur : nom non vide, montant strictement positif (`GreaterThan(0)`).
4. Pourquoi le client reçoit-il toutes les erreurs plutôt que la première seulement ? Quelle ligne de `ValidationBehavior` en est responsable ?

## Pour aller plus loin

- [Documentation de FluentValidation](https://docs.fluentvalidation.net/)
