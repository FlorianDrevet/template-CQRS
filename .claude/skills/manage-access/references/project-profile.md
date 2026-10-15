# project-profile.json

Fichier à la racine du projet généré. Écrit par `init-project`, maintenu par `manage-access` (et relu par `add-feature`). Il porte le **sens métier** des rôles, groupes et personas. Le code (`AppRoles.cs`, `GroupOptions.cs`, policies) et le realm restent la source de vérité **technique** : en cas de divergence, ils font foi, signalez l'écart et corrigez le profil.

## Schéma (schemaVersion 1)

```json
{
  "schemaVersion": 1,
  "name": "Contoso.Orders",
  "authorizationModel": "AppRolesAndGroups",
  "options": { "aspire": true, "rateLimiting": true, "tests": true },
  "roles": [
    { "name": "Admin", "description": "Administre le catalogue" }
  ],
  "groups": [
    {
      "name": "Administrators",
      "description": "Administrateurs de l'application",
      "entraObjectId": null,
      "localObjectId": "6f1c2a4e-0d3b-4c1e-9a57-2b8f0e6d4c11"
    }
  ],
  "personas": [
    {
      "username": "admin@local.test",
      "firstName": "Alice",
      "lastName": "Admin",
      "roles": ["Admin"],
      "groups": ["Administrators"],
      "purpose": "Tester les routes réservées aux administrateurs"
    }
  ]
}
```

| Champ | Règle |
|-------|-------|
| `schemaVersion` | `1`. |
| `name` | Nom du projet généré (PascalCase avec points), identique à celui des dossiers `src/<Nom>.*`. |
| `authorizationModel` | `AppRoles`, `Groups`, `AppRolesAndGroups` ou `Authenticated`. |
| `options` | `aspire`, `rateLimiting`, `tests` : booléens, reflètent les options de génération. |
| `roles[].name` | Valeur exacte de la constante `AppRoles` et du rôle du realm. Liste vide si le modèle n'a pas de rôles. |
| `roles[].description` | Une phrase : ce que le rôle permet. |
| `groups[].name` | Nom logique, identique à la propriété de `GroupOptions` et au `name` du groupe du realm. Liste vide si le modèle n'a pas de groupes. |
| `groups[].entraObjectId` | Object ID du groupe Entra de production ; `null` tant qu'il n'est pas connu. Information non secrète, mais ne l'inventez jamais. |
| `groups[].localObjectId` | GUID de `objectId` dans le realm et de `appsettings.Development.json`. `null` sans Aspire. |
| `personas[]` | Utilisateurs du realm local : `username`, `firstName`, `lastName`, `roles` (noms), `groups` (noms logiques, sans `/`), `purpose` (à quoi sert ce compte de test). Liste vide sans Aspire. Jamais de mot de passe dans le profil. |

## Règles de mise à jour

- Après chaque opération réussie (étape 7 du skill), répercutez l'ajout, le renommage ou le retrait ; gardez l'ordre existant et le format (JSON indenté de 2 espaces, UTF-8, fin de ligne finale).
- Ajouter un rôle ou un groupe : demandez la `description` si elle n'est pas donnée ; ne la devinez pas à partir du seul nom.
- Ajouter un persona : renseignez `purpose`.
- Retirer ou renommer : mettez à jour toutes les références croisées (`personas[].roles`, `personas[].groups`).
- `entraObjectId` : renseignez-le seulement si l'utilisateur le fournit.
- Si le fichier est absent, reconstruisez-le depuis le code et le realm (modèle d'après les fichiers présents, rôles d'après `AppRoles.cs`, groupes d'après `GroupOptions.cs` et le realm, personas d'après le realm), avec `description` / `purpose` vides à compléter (`""`), et dites-le à l'utilisateur.
- Si le fichier existe mais n'est pas du JSON valide ou a un `schemaVersion` inconnu, ne l'écrasez pas : signalez-le et demandez.
- Validation : `pwsh -NoProfile -Command "Get-Content project-profile.json -Raw | ConvertFrom-Json | Out-Null; 'JSON OK'"` (ou `jq . project-profile.json`).
