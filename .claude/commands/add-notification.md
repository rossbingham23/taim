Add a new notification kind to TAIM.

## Steps

1. **Add to the enum** in `Taim.Core/Notifications/INotificationChannel.cs`:
   ```csharp
   public enum NotificationKind { ..., YourNewKind }
   ```

2. **Add to frontend types** in `src/frontend/taim-web/src/lib/types.ts`:
   ```ts
   export type NotificationKind = ... | 'your_new_kind'
   ```
   Note: the SignalR serializer uses `SnakeCaseLower`, so `YourNewKind` → `"your_new_kind"`.

3. **Emit it** from a service or orchestrator using `INotificationService.NotifyAsync`:
   ```csharp
   await notifications.NotifyAsync(tenantId, NotificationKind.YourNewKind,
       title, body, metadata, ct);
   ```

4. **React to it** in the frontend via `onNotification`:
   ```ts
   onNotification(n => {
     if (n.kind === 'your_new_kind') { /* handle */ }
   })
   ```

5. **Update CLAUDE.md**:
   - `Taim.Core/CLAUDE.md` — add to NotificationKind list
