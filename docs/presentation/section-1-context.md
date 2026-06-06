---
marp: true
theme: uncover
class: invert
paginate: true
style: |
  section { font-family: 'Segoe UI', system-ui, sans-serif; }
  h1 { font-size: 2.2em; }
  h2 { font-size: 1.6em; margin-bottom: 0.3em; }
  h3 { font-size: 1.1em; }
  p, li { font-size: 0.7em; line-height: 1.6; }
  table { font-size: 0.55em; margin: 0 auto; }
  .big { font-size: 1.5em; font-weight: 700; }
  .cols { display: flex; gap: 2em; justify-content: center; align-items: flex-start; }
  .cols ul { flex: 1; }
  .card { border: 1px solid #444; border-radius: 12px; padding: 1.5em; margin: 0.5em 0; }
  .stat { text-align: center; }
  .stat-num { font-size: 2.5em; font-weight: 800; color: #4f46e5; }
  .stat-label { font-size: 0.65em; color: #aaa; margin-top: -0.3em; }
  .check { color: #22c55e; }
  .cross { color: #ef4444; }
---

# Section 1
## Contexte et Problématique

Le marché du recrutement · Les systèmes ATS · Pourquoi Propel

---

## 1.1 Contexte

### Un marché du recrutement ultra-concurrentiel

<div class="cols">
<div class="stat">
<div class="stat-num">250</div>
<div class="stat-label">CV reçus en moyenne<br>par offre d'emploi</div>
</div>
<div class="stat">
<div class="stat-num">75%</div>
<div class="stat-label">éliminés par les ATS<br>avant lecture humaine</div>
</div>
<div class="stat">
<div class="stat-num">2%</div>
<div class="stat-label">taux de convocation<br>aux entretiens</div>
</div>
</div>

Les systèmes **ATS** (Applicant Tracking Systems) trient automatiquement les CV avant toute intervention humaine.

---

## Le problème du CV générique

<div class="cols">
<div>

<span class="cross">✗</span> **CV unique** pour toutes les candidatures

- Même contenu, même format
- Aucune adaptation aux mots-clés de l'offre
- Faible score ATS
- Taux de rejet élevé

</div>
<div>

<span class="check">✓</span> **CV personnalisé** par offre

- Contenu adapté aux exigences du poste
- Mots-clés optimisés pour l'ATS
- Meilleur taux de passage
- Candidature plus pertinente

</div>
</div>

---

## 1.2 Problématiques Identifiées

<div class="card">

**🕐 Temps important** — Adapter manuellement un CV pour chaque offre prend **30 à 60 minutes** par candidature.

</div>

<div class="card">

**📉 Taux de rejet élevé** — Les CV non personnalisés sont rejetés par les ATS dans **75% des cas** avant lecture humaine.

</div>

<div class="card">

**🔍 Lacunes invisibles** — Difficulté à identifier les compétences manquantes vis-à-vis d'une offre spécifique.

</div>

<div class="card">

**📂 Absence de centralisation** — Aucun espace unique pour gérer l'ensemble de son parcours professionnel.

</div>

<div class="card">

**📋 Suivi dispersé** — Les candidatures sont suivies sur des outils disparates (Excel, emails, notes).

</div>

---

## 1.3 Objectifs du Projet

<div class="cols">
<div>

### Objectifs Métier

- **Centraliser** les informations professionnelles de l'utilisateur
- **Automatiser** l'adaptation des CV aux offres d'emploi
- **Faciliter** le suivi des candidatures
- **Améliorer** la compatibilité ATS des CV générés

</div>
<div>

### Objectifs Techniques

- Architecture **microservices** scalable
- Pipeline **IA** pour la génération de CV
- Base de données **vectorielle** (pgvector)
- Communication **event-driven** (Kafka)
- Authentification **SSO** (Keycloak)

</div>
</div>

---

## Résumé de la Section 1

- Le marché du recrutement est **ultra-concurrentiel** (250 CV/offre)
- Les systèmes **ATS** éliminent 75% des CV automatiquement
- Le **CV générique** est la principale cause de rejet
- Cinq problématiques majeures identifiées : temps, rejet, lacunes, centralisation, suivi
- Objectif : **automatiser la personnalisation des CV** via l'IA

➡️ Section 2 : Analyse de l'existant et positionnement
