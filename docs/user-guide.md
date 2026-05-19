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

## Meetings

After your executive team completes their kickoff strategies, the CEO automatically calls a **Kickoff Sync** meeting. All executives participate in a turn-based conversation where the CEO drives the agenda, each executive responds, and the meeting closes with a summary and concrete action items.

You'll see a **Meetings** section appear in the Task page once the meeting completes:

- Each meeting card shows the meeting type, topic, message count, and a summary excerpt
- Click **View** to read the full transcript — each message is attributed to the agent who spoke it
- Action items generated during the meeting appear in the Actions panel alongside delegations from the kickoff

Meetings run fully autonomously — you don't participate. They typically produce 4–12 messages and run for 30–90 seconds depending on team size.

---

## Agent Work Loop (Sprint 3)

After kickoff completes, agents automatically begin executing their assigned Actions. You don't need to do anything — work starts within a few seconds of the kickoff finishing.

### Action Status Colors

The Actions panel in the team view shows a colored left border for each action:

| Color | Status | Meaning |
|---|---|---|
| **Blue** | `open` | Waiting to be picked up |
| **Amber** | `in_progress` | An agent is actively working on it |
| **Green** | `done` | Completed successfully |
| **Red** | `blocked` | Agent couldn't continue (needs approval or hit an error) |

### How Agents Execute Actions

Each agent runs a multi-turn loop:
1. Receives the action title and description as a task
2. Uses available tools (web search for all agents; code writing for Developer/QA agents)
3. Calls `complete_task` when finished — setting the action to `done` or `blocked`
4. If the agent reaches 15 LLM turns without finishing, the action is automatically blocked

### Unblocking a Blocked Action

If an agent needs to use a tool that requires your approval (e.g., sending an email, making a file change), it will:
1. Create an approval request visible in the **Approvals** queue
2. Set the action to `blocked` and its own status to `WaitingApproval`

To unblock:
1. Go to the **Approvals** tab and approve or deny the tool call
2. Use the action's **Execute** button (via `POST /api/actions/{id}/execute`) to re-trigger the agent

The agent resumes from where it left off using saved conversation history.

---

## Coming Soon

- **KPI Dashboard** _(Sprint 5)_: See each agent's KPIs and track progress over time.
