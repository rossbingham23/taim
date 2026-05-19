Add a new executive agent role to TAIM.

## Steps

1. **Add the role** to `AgentRole` enum in `Taim.Core/Agents/AgentModels.cs`:
   ```csharp
   public enum AgentRole { ..., YourNewRole }
   ```

2. **Create the agent class** in `Taim.Agents/Executive/YourNewRoleAgent.cs`:
   ```csharp
   public sealed class YourNewRoleAgent(IChatClient chatClient) : ExecutiveAgentBase(chatClient)
   {
       protected override string RoleTitle => "Your Role Title";
       protected override string RoleDescription => "What this executive does and owns.";
   }
   ```

3. **Wire into AgentOrchestrator** in `Taim.Agents/Shared/AgentOrchestrator.cs`, `InstantiateAgent` method:
   ```csharp
   AgentRole.YourNewRole => new YourNewRoleAgent(client),
   ```

4. **Wire into AgentFactory** in `Taim.Agents/Shared/AgentFactory.cs`, `MapRole` method (if it exists).

5. **Update BootstrapAgent** in `Taim.Agents/Bootstrap/BootstrapAgent.cs` — ensure the system prompt mentions the new role so the LLM recommends it in appropriate scenarios.

6. **Update CLAUDE.md**:
   - `Taim.Core/CLAUDE.md` — add to AgentRole enum list
   - `Taim.Agents/CLAUDE.md` — add to Executive Agents table

## Notes

- Executive agents are NOT registered in DI — always instantiated directly with `new`
- The role enum value is stored in DB as lowercase string: `YourNewRole` → `"yournewrole"`
- The role is serialized over HTTP as camelCase: `YourNewRole` → `"yourNewRole"`
