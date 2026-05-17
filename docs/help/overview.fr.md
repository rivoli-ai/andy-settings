---
title: Présentation d'Andy Settings
slug: andy-settings-overview
order: 1
tags: [settings, configuration, secrets]
---

# Présentation d'Andy Settings

Andy Settings est le registre centralisé des réglages pour l'ensemble de l'écosystème Andy. Il possède les *définitions* de réglages (lues depuis le `registration.json` de chaque service au démarrage), les *valeurs* de réglages (par installation), et le coffre-fort de secrets qui soutient les identifiants de chaque service.

## Ce qu'il fait

- Initialise les définitions de réglages depuis le bloc `settings.definitions` du `registration.json` de chaque service frère au démarrage.
- Stocke les valeurs de réglage par installation et les sert via l'API `ISetting` qu'utilisent tous les services Andy.
- Agit comme coffre-fort de secrets central — les PAT, les clés d'API et autres identifiants partagés résident ici exactement une fois. Les services consommateurs détiennent des références (p. ex. `${secret:github.token}`), pas la valeur brute.
- Publie les événements de changement sur NATS pour que les services dépendants se rafraîchissent en quelques secondes.

## Concepts clés

- **Définition vs valeur** — les définitions sont un schéma (nom, type de donnée, valeur par défaut) ; les valeurs sont spécifiques à l'utilisateur/installation.
- **Référence de secret** — un placeholder `${secret:<clé>}` qu'un service résout via `ISecretStore`. Le secret réel ne quitte jamais Settings.
- **Portée du réglage** — globale, par organisation, ou par utilisateur. La plupart des réglages sont globaux ; les jetons sont par utilisateur.

## Où il s'intègre

Settings est une dépendance dure pour tous les autres services Andy — sans lui, les services ne peuvent pas charger leur configuration. Conductor lit les clés de fournisseur, les PAT GitHub et les bascules de fonctionnalité à travers lui.

## Configuration

Auto-amorcé : Settings lit son propre `registration.json` et s'initialise lui-même en premier. Les chaînes de connexion proviennent de variables d'environnement intégrées dans le bundle de service Conductor.

## Dépannage

- **Un service ne trouve pas sa config** — Settings est injoignable ou n'a pas fini son ensemencement. Vérifiez `andy-settings.log` pour les lignes `Seeded N definitions from M services`.
- **Erreurs « Secret not found »** — la référence de secret pointe vers une clé qui n'a jamais été écrite. Définissez-la via **Réglages → Catalogues → Services → Andy Settings → Secrets** ou via l'UI du fournisseur approprié.
- **Changements de Settings non répercutés** — NATS n'est pas en cours d'exécution ou le consommateur n'est pas abonné. Redémarrez le service consommateur ; les valeurs sont rafraîchies avec empressement à la prochaine requête.
