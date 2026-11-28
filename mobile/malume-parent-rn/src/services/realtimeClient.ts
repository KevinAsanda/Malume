import {
  DriverTelemetryPingDto,
  EtaCascadeUpdateDto,
  SosAlertDto,
} from '../types/api';
import {
  parseSignalRFrames,
  SIGNALR_RECORD_SEPARATOR,
} from '../utils/formatters';

export interface ParentRealtimeHandlers {
  onConnected?: () => void;
  onDisconnected?: () => void;
  onLocationUpdated?: (ping: DriverTelemetryPingDto) => void;
  onEtaCascade?: (update: EtaCascadeUpdateDto) => void;
  onTripStatusChanged?: (payload: { tripId: string; status: string }) => void;
  onHandoverRecorded?: (payload: {
    tripId: string;
    learnerId: string;
    attendanceStatus: string;
    handoverMethod: string;
  }) => void;
  onSosAlert?: (alert: SosAlertDto) => void;
}

export class ParentRealtimeClient {
  private socket: WebSocket | null = null;
  private invocationId = 1;
  private joinedTrips = new Set<string>();
  private handshakeComplete = false;
  private handlers: ParentRealtimeHandlers = {};

  async connect(
    apiBaseUrl: string,
    accessToken: string,
    tripIds: string[],
    handlers: ParentRealtimeHandlers,
  ): Promise<void> {
    this.disconnect();
    this.handlers = handlers;

    const cleanBase = apiBaseUrl.trim().replace(/\/+$/, '');
    const negotiateResp = await fetch(`${cleanBase}/hubs/trips/negotiate?negotiateVersion=1`, {
      method: 'POST',
      headers: {
        Authorization: `Bearer ${accessToken}`,
      },
    });

    if (!negotiateResp.ok) {
      return;
    }

    const negotiated = (await negotiateResp.json()) as { connectionToken?: string };
    const wsScheme = cleanBase.startsWith('https://') ? 'wss://' : 'ws://';
    const hostPath = cleanBase.replace(/^https?:\/\//, '');
    const tokenParam = encodeURIComponent(accessToken);
    const connParam = negotiated.connectionToken
      ? `&id=${encodeURIComponent(negotiated.connectionToken)}`
      : '';
    const wsUrl = `${wsScheme}${hostPath}/hubs/trips?access_token=${tokenParam}${connParam}`;

    this.socket = new WebSocket(wsUrl);
    this.socket.onopen = () => {
      this.handshakeComplete = false;
      this.socket?.send(
        `${JSON.stringify({ protocol: 'json', version: 1 })}${SIGNALR_RECORD_SEPARATOR}`,
      );
    };

    this.socket.onmessage = event => {
      const raw = typeof event.data === 'string' ? event.data : '';
      const frames = parseSignalRFrames(raw);

      if (!this.handshakeComplete) {
        this.handshakeComplete = true;
        this.invoke('JoinParentNotificationsGroup', []);
        for (const tid of tripIds) {
          this.joinTrip(tid);
        }
        this.handlers.onConnected?.();
      }

      for (const frame of frames) {
        if (frame.type !== 1 || !frame.target || !frame.arguments) {
          continue;
        }
        const payload = frame.arguments[0];
        switch (frame.target) {
          case 'LocationUpdated':
            this.handlers.onLocationUpdated?.(payload as DriverTelemetryPingDto);
            break;
          case 'EtaCascadeUpdated':
            if (Array.isArray(payload)) {
              for (const item of payload) {
                this.handlers.onEtaCascade?.(item as EtaCascadeUpdateDto);
              }
            } else {
              this.handlers.onEtaCascade?.(payload as EtaCascadeUpdateDto);
            }
            break;
          case 'TripStatusChanged':
            this.handlers.onTripStatusChanged?.(payload as { tripId: string; status: string });
            break;
          case 'HandoverRecorded':
            this.handlers.onHandoverRecorded?.(
              payload as {
                tripId: string;
                learnerId: string;
                attendanceStatus: string;
                handoverMethod: string;
              },
            );
            break;
          case 'SosIncidentCreated':
            this.handlers.onSosAlert?.(payload as SosAlertDto);
            break;
        }
      }
    };

    this.socket.onclose = () => {
      this.handshakeComplete = false;
      this.handlers.onDisconnected?.();
    };
  }

  joinTrip(tripId: string): void {
    if (this.joinedTrips.has(tripId)) {
      return;
    }
    this.joinedTrips.add(tripId);
    if (this.handshakeComplete) {
      this.invoke('JoinTripGroup', [tripId]);
    }
  }

  disconnect(): void {
    this.joinedTrips.clear();
    this.handshakeComplete = false;
    if (this.socket) {
      try {
        this.socket.close();
      } catch {
        // Ignore close errors
      }
      this.socket = null;
    }
  }

  private invoke(target: string, args: unknown[]): void {
    if (!this.socket || this.socket.readyState !== 1) {
      return;
    }
    const id = String(this.invocationId++);
    const msg = JSON.stringify({
      type: 1,
      invocationId: id,
      target,
      arguments: args,
    });
    this.socket.send(`${msg}${SIGNALR_RECORD_SEPARATOR}`);
  }
}
