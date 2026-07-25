---
title: Présentation d'Andy Settings
slug: andy-settings-overview
order: 1
tags: [settings, configuration, secrets]
---

# Présentation d'Andy Settings

Andy Settings est le registre centralisé des réglages pour l'ensemble de l'écosystème Andy. Il possède les *définitions* de réglages (lues depuis le `registration.json` de chaque service au démarrage), les *valeurs* de réglages (portées par installation, utilisateur, équipe et davantage), et le magasin de secrets chiffrés qui soutient les identifiants de chaque service.

## Ce qu'il fait

- Réconcilie les définitions de réglages depuis le bloc `settings.definitions` du `registration.json` de chaque service frère au démarrage — et pas seulement au premier lancement, de sorte qu'une modification du manifeste est appliquée au démarrage suivant.
- Stocke les valeurs de réglage portées et les sert via une API REST. Les consommateurs .NET utilisent le paquet `Andy.Settings.Client` (`IAndySettingsClient`), qui résout les valeurs et met en cache un instantané.
- Agit comme magasin de secrets central — les PAT, les clés d'API et autres identifiants partagés résident ici exactement une fois, chiffrés au repos. Les services consommateurs les lisent via l'API des secrets et doivent détenir la permission `secret:read` pour le faire.
- Publie les événements de changement sur NATS pour que les services dépendants se rafraîchissent en quelques secondes.

## Concepts clés

- **Définition vs valeur** — les définitions sont un schéma (clé, type de donnée, valeur par défaut, validation) ; les valeurs sont les affectations concrètes.
- **Les secrets sont stockés à part des valeurs** — un réglage adossé à un secret est rejeté par l'API ordinaire des valeurs. Sa valeur réside dans un stockage chiffré indexé par définition et par portée, n'est renvoyée qu'aux appelants détenant `secret:read`, et chaque déchiffrement est consigné dans la piste d'audit.
- **Portée du réglage** — sept niveaux, de la précédence la plus faible à la plus forte : `Machine`, `Application`, `Service`, `User`, `Team`, `Workspace`, `RuntimeOverride`. La portée de plus forte précédence possédant une valeur l'emporte ; si aucune n'en possède, la valeur par défaut de la définition est utilisée. Toutes les portées sauf `Machine` exigent un identifiant de portée.

## Où il s'intègre

Settings est une dépendance dure pour tous les autres services Andy — sans lui, les services ne peuvent pas charger leur configuration. Conductor lit les clés de fournisseur, les PAT GitHub et les bascules de fonctionnalité à travers lui.

## Configuration

Auto-amorcé : Settings lit son propre `registration.json` et s'initialise lui-même en premier. Les chaînes de connexion proviennent de variables d'environnement intégrées dans le bundle de service Conductor. Les migrations de schéma et l'ensemencement des définitions s'exécutent à chaque démarrage, dans tous les environnements.

## Dépannage

- **Un service ne trouve pas sa config** — Settings est injoignable ou n'a pas fini sa réconciliation. Vérifiez dans `andy-settings.log` la ligne `Definition catalog reconciled: N added, M updated, ...`, journalisée une fois la réconciliation de démarrage terminée.
- **Erreurs « Secret not found »** — aucun secret n'a été écrit pour cette définition à la portée demandée. Notez que les secrets sont par portée : une valeur définie pour un utilisateur n'est pas visible par un autre. Définissez-la via **Réglages → Catalogues → Services → Andy Settings → Secrets** ou via l'UI du fournisseur approprié.
- **Un secret apparaît manquant juste après un redémarrage** — si le trousseau de clés Data Protection a changé, le chiffré existant ne peut plus être déchiffré et est signalé comme absent plutôt que de faire échouer l'appelant. Le journal enregistre `[SECRET-UNDECRYPTABLE]`. Redéfinissez le secret pour le réparer.
- **Changements de Settings non répercutés** — NATS n'est pas en cours d'exécution ou le consommateur n'est pas abonné. Redémarrez le service consommateur ; les valeurs sont rafraîchies à la lecture suivante.
