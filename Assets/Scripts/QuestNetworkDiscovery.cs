using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Discovers the Heisenberg PC on the local network from the Quest.
/// Sends a UDP broadcast every few seconds and stores the IP of the PC that answers.
/// </summary>
public sealed class QuestNetworkDiscovery : MonoBehaviour
{
    private const string DiscoveryMessage = "HEISENBERG_LOOKING";
    private const string PcReadyMessage = "PC_READY";

    [Header("Discovery")]
    [SerializeField] private int discoveryPort = 47777;
    [SerializeField] private float broadcastIntervalSeconds = 2f;
    [SerializeField] private bool stopAfterFirstPcFound = true;

    [Header("Runtime")]
    [SerializeField] private string discoveredPcIp = string.Empty;

    private readonly object stateLock = new object();

    private CancellationTokenSource cancellationSource;
    private UdpClient udpClient;
    private Task discoveryTask;

    public string DiscoveredPcIp
    {
        get
        {
            lock (stateLock)
            {
                return discoveredPcIp;
            }
        }
    }

    public bool HasDiscoveredPc => !string.IsNullOrWhiteSpace(DiscoveredPcIp);

    private void OnEnable()
    {
        StartDiscovery();
    }

    private void OnDisable()
    {
        StopDiscovery();
    }

    private void OnDestroy()
    {
        StopDiscovery();
    }

    public void StartDiscovery()
    {
        if (discoveryTask != null && !discoveryTask.IsCompleted)
        {
            return;
        }

        UdpClient client = new UdpClient();
        client.EnableBroadcast = true;

        cancellationSource = new CancellationTokenSource();
        udpClient = client;
        discoveryTask = RunDiscoveryAsync(client, cancellationSource.Token);
    }

    public void StopDiscovery()
    {
        CancellationTokenSource source = cancellationSource;
        UdpClient client = udpClient;
        Task task = discoveryTask;

        source?.Cancel();

        // Closing the socket interrupts any pending ReceiveAsync call immediately.
        client?.Close();
        client?.Dispose();
        udpClient = null;
        discoveryTask = null;

        DisposeCancellationSourceWhenTaskFinishes(source, task);
        cancellationSource = null;
    }

    private async Task RunDiscoveryAsync(UdpClient client, CancellationToken cancellationToken)
    {
        using CancellationTokenSource linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        CancellationToken activeToken = linkedCancellation.Token;

        Task receiveTask = ReceivePcResponsesAsync(client, linkedCancellation);
        byte[] discoveryPayload = Encoding.UTF8.GetBytes(DiscoveryMessage);
        IPEndPoint broadcastEndPoint = new IPEndPoint(IPAddress.Broadcast, discoveryPort);
        TimeSpan broadcastInterval = TimeSpan.FromSeconds(Math.Max(0.1f, broadcastIntervalSeconds));

        while (!activeToken.IsCancellationRequested)
        {
            try
            {
                await client.SendAsync(discoveryPayload, discoveryPayload.Length, broadcastEndPoint)
                    .ConfigureAwait(false);

                await Task.Delay(broadcastInterval, activeToken).ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (activeToken.IsCancellationRequested)
                {
                    break;
                }

                await DelayBeforeRetryAsync(broadcastInterval, activeToken).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }

        linkedCancellation.Cancel();
        client.Close();

        try
        {
            await receiveTask.ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
            // Normal shutdown path: closing the socket releases ReceiveAsync.
        }
        catch (SocketException)
        {
            // Normal shutdown path on platforms that report a closed UDP socket as a socket error.
        }
    }

    private async Task ReceivePcResponsesAsync(UdpClient client, CancellationTokenSource linkedCancellation)
    {
        while (!linkedCancellation.IsCancellationRequested)
        {
            try
            {
                UdpReceiveResult result = await client.ReceiveAsync().ConfigureAwait(false);
                string message = Encoding.UTF8.GetString(result.Buffer);

                if (!string.Equals(message, PcReadyMessage, StringComparison.Ordinal))
                {
                    continue;
                }

                lock (stateLock)
                {
                    discoveredPcIp = result.RemoteEndPoint.Address.ToString();
                }

                if (stopAfterFirstPcFound)
                {
                    linkedCancellation.Cancel();
                    client.Close();
                    break;
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (SocketException)
            {
                if (linkedCancellation.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private static void DisposeCancellationSourceWhenTaskFinishes(CancellationTokenSource source, Task task)
    {
        if (source == null)
        {
            return;
        }

        if (task == null || task.IsCompleted)
        {
            source.Dispose();
            return;
        }

        task.ContinueWith(
            completedTask =>
            {
                _ = completedTask.Exception;
                source.Dispose();
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task DelayBeforeRetryAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException)
        {
            // Normal shutdown path when leaving Play Mode or disabling the component.
        }
    }
}