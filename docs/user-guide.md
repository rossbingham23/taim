# TAIM User Guide

TAIM — Team AI Manager. You describe an outcome, TAIM assembles and runs an AI organisation to work toward it.

---

## Getting Started

### 1. Log In

Open http://localhost:3000. You'll be redirected to the login page.

- Email: `admin@taim.local`
- Password: `taim-admin`

### 2. Submit a Goal

After login you land on the Goals page. Type your goal in the text area — be specific and outcome-focused:

> **Good:** "Build a mobile fitness tracking app with social features and an AI coaching component"
> **Too vague:** "Build an app"

Set a budget in USD (controls how much the team can spend on LLM calls). Click **Launch AI Team**.

### 3. Watch the Team Assemble

You're taken to the Task page. The system is now:
1. Researching your goal domain
2. Designing an executive team suited to your goal
3. Creating each agent and assigning them a charter

Status shows **bootstrapping** while this happens (typically 30–60 seconds).

---

## The Task Page

### Team Structure

Once assembled, you'll see an org-chart (if agents have a hierarchy) and a card grid showing each agent:
- **Name** — the agent's chosen name
- **Role** — CEO, CTO, CMO, CFO, HR, etc.
- **Status** — idle, active, waiting approval, sleeping, terminated
- **Provider / Model** — which LLM is powering this agent

Click any agent card to see their details in the sidebar.

### Actions Panel (Sprint 1)

After kickoff, the sidebar shows the **Actions** panel — work items that the executives have delegated to each other. Each action shows:
- A colored left border: **blue** = open, **amber** = in progress, **red** = blocked, **green** = done
- The action title (what needs to be done)
- The assigned agent's name

Actions are created automatically from each agent's kickoff delegations. In future sprints, agents will claim and execute these actions automatically.

### Activity Console

The activity feed shows everything happening in real time:
- `LOG` — agent is doing something (proposing KPIs, running strategy, etc.)
- `STATUS` — an agent changed status (activated, idle)
- `REPORT` — an executive report was generated
- `TEAM` — the team changed (agent added, etc.)
- `ACTION` — an action was created or updated
- `MEETING` — a meeting started or completed _(Sprint 2)_

Click the ▼ button next to "Activity" to collapse the console.

### Reports Page

Navigate to **Reports** in the sidebar nav. Each executive generates a kickoff strategy report containing:
- **Analysis** — their strategic assessment
- **Decision** — their key priorities
- **Actions** — concrete first steps
- **Delegations** — what they're asking their team to do

---

## Approvals

Some agent actions require your approval before they're executed. When an agent needs approval, you'll see a notification and the **Approvals** page will show a pending item.

Each approval shows:
- Which agent is requesting
- What tool/action they want to use
- The specific parameters

You can:
- **Approve once** — allow this specific action
- **Approve always** (for this agent + tool) — trust the agent with this type of action going forward
- **Deny** — the agent is told the action was denied and must find another approach

---

## Console Page

The **Console** nav item shows the system-wide event feed — all events from all tasks, all agents. Useful for debugging or monitoring multiple goals at once.

---

## Settings

Configure the LLM provider TAIM uses to power your agents:
- **Provider**: Anthropic (Claude), OpenAI (GPT-4), Google (Gemini), Ollama (local)
- **API Key**: your key for the chosen provider
- **Default Model**: the model used for all agents

---

## Coming Soon

- **Meetings** _(Sprint 2)_: Agents hold structured conversations to align strategy, resolve blockers, and report progress. You'll see meeting transcripts and action items in the Task page.
- **Agent Work Loop** _(Sprint 3)_: Agents automatically claim Actions and execute them — writing code, doing research, drafting documents.
- **KPI Dashboard** _(Sprint 5)_: See each agent's KPIs and track progress over time.
