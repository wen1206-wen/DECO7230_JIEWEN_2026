# DECO7230_JIEWEN_2026

# DECO7230 – IdeaSpace XR

**Course:** DECO7230 – Digital Prototyping and Extended Reality
**Semester:** Semester 2, 2026

## Project Overview

IdeaSpace XR is an immersive brainstorming and idea-organisation workspace inspired by digital collaboration tools such as Miro.

The project explores how spatial interaction in XR can support users when organising ideas. Instead of using a mouse and a 2D interface, users can directly grab and move sticky notes within a three-dimensional workspace.

The current prototype focuses on a simple idea-sorting task.

---

# Prototype 2A

Prototype 2A investigates direct XR interaction with virtual sticky notes.

Users are presented with **six unsorted sticky notes** and three labelled category boards:

* Problems
* Positive Findings
* Opportunities

There are two sticky notes belonging to each category.

The main interaction flow is:

1. View the unsorted sticky notes.
2. Select or grab a sticky note using an XR controller.
3. Move the sticky note through the virtual environment.
4. Place it onto one of the three category boards.
5. Repeat the process until all six sticky notes have been organised.
6. Reposition a sticky note if the user believes it has been placed incorrectly.

---

## Prototype 2A Research Focus

Prototype 2A focuses on whether users can understand and perform the following interaction:

**See → Grab → Move → Categorise → Place**

The prototype is intended to investigate:

* Whether users recognise that sticky notes are interactive.
* Whether users understand how to grab and move a sticky note.
* Whether the three category areas are understandable.
* Whether users can successfully place sticky notes into the intended categories.
* Whether users understand how to correct a misplaced note.
* Whether interaction feedback is clear enough.

---

## Prototype Development

Prototype 2A is developed in Unity using XR interaction components.

Current prototype features include:

* XR Origin / XR Rig
* Left and right XR controllers
* XR Interaction Manager
* Locomotion
* Six interactive sticky notes
* Three category boards
* World-space interface elements
* Sticky-note grab interaction
* Sticky-note placement and organisation

The prototype is designed primarily for Meta Quest testing.

---

## Prototype 2A Content

### Problems

Two sticky notes represent problems identified during brainstorming.

### Positive Findings

Two sticky notes represent positive findings.

### Opportunities

Two sticky notes represent possible opportunities or future ideas.

A total of **six sticky notes** are used in the prototype.

Reducing the number of notes keeps the prototype focused on testing the interaction rather than requiring participants to complete a long sorting task.

---

# Evaluation

Prototype 2A will be evaluated through formative usability testing.

Approximately **5 participants** will be invited to test the prototype.

Participants will complete the main sorting task while the researcher observes their interaction.

The evaluation focuses on:

* Task completion
* Interaction errors
* Incorrect note placement
* Number of prompts/help required
* Interaction hesitation
* Understanding of categories
* Overall usability

Detailed testing materials are located in the `Testing` folder.

---

## Success Criteria

The prototype will be considered successful if:

* At least **4 out of 5 participants** successfully organise all six notes.
* At least **4 out of 5 participants** complete the task without direct assistance.
* Participants make no more than approximately **one incorrect placement** during the task.
* Participants require no more than approximately **one researcher prompt**.
* Most participants rate the interaction as understandable and easy to use.

These criteria are intended as formative indicators rather than final validation of the system.

---

# Repository Structure

```text
DECO7230/
│
├── README.md
├── Prototype1/
├── Prototype2a/
├── Evaluation1/
└── Testing/
    ├── testing-plan.md
    ├── questionnaire.md
    ├── observation-sheet.md
    ├── results.md
    └── findings.md
```

---

# Prototype Progress

## Prototype 1

Prototype 1 explored the basic concept of spatial idea organisation and tested whether users understood the idea of moving content from an unsorted area into organised categories.

## Prototype 2A

Prototype 2A introduces direct XR interaction using controllers and tests the physical interaction of grabbing, moving and categorising sticky notes.

Further prototype iterations will respond to findings from the Prototype 2A usability testing.

---

# Tools

* Unity
* XR Interaction Toolkit
* Meta Quest / XR environment
* GitHub
* GitHub Desktop

---

# Current Status

Prototype 2A is currently being prepared for formative usability testing.

Testing results, observations and design findings will be added to the repository after participant testing.
