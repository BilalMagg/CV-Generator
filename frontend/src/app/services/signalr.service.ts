import { Injectable, OnDestroy } from '@angular/core';
import * as signalR from '@microsoft/signalr';

@Injectable({
  providedIn: 'root'
})
export class SignalRService implements OnDestroy {
  private connection: signalR.HubConnection | null = null;

  ngOnDestroy(): void {
    this.disconnect();
  }

  async connect(): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) return;

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/jobs')
      .withAutomaticReconnect()
      .build();

    this.connection.onreconnecting(() => console.warn('SignalR reconnecting...'));
    this.connection.onreconnected(() => console.info('SignalR reconnected'));
    this.connection.onclose(() => console.info('SignalR closed'));

    await this.connection.start();
  }

  async disconnect(): Promise<void> {
    if (this.connection) {
      await this.connection.stop();
      this.connection = null;
    }
  }

  async joinGroup(searchId: string): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('JoinSearchGroup', searchId);
    }
  }

  async leaveGroup(searchId: string): Promise<void> {
    if (this.connection?.state === signalR.HubConnectionState.Connected) {
      await this.connection.invoke('LeaveSearchGroup', searchId);
    }
  }

  onJobArrived(callback: (job: any) => void): void {
    this.connection?.on('JobArrived', callback);
  }

  onSearchFinished(callback: (data: any) => void): void {
    this.connection?.on('SearchFinished', callback);
  }

  offJobArrived(): void {
    this.connection?.off('JobArrived');
  }

  offSearchFinished(): void {
    this.connection?.off('SearchFinished');
  }
}
