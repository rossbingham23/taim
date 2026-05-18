import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import type { Notification } from './types'

export type NotificationHandler = (notification: Notification) => void

let connection: HubConnection | null = null
const handlers = new Set<NotificationHandler>()

export async function connectSignalR(token: string): Promise<void> {
  if (connection) return

  connection = new HubConnectionBuilder()
    .withUrl('/hubs/agents', { accessTokenFactory: () => token })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  connection.on('Notification', (notification: Notification) => {
    handlers.forEach(h => h(notification))
  })

  await connection.start()
}

export function disconnectSignalR(): Promise<void> {
  const conn = connection
  connection = null
  return conn?.stop() ?? Promise.resolve()
}

export function onNotification(handler: NotificationHandler): () => void {
  handlers.add(handler)
  return () => handlers.delete(handler)
}
