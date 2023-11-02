using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Http;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Relay;
using NetworkEvent = Unity.Networking.Transport.NetworkEvent;
using TMPro;
using UnityEngine.UI;

public class RelayManagement : MonoBehaviour
{
    const int m_MaxConnections = 4;

    public string RelayJoinCode;
    public GameObject playerPrefab;
    public TMP_Text ShowingJoinCode;
    public TMP_InputField inputField;
    public GameObject inputFieldObject;
    public GameObject BtnPanel;

    void Start()
    {
        Example_AuthenticatingAPlayer();
    }
    public void RelayHostStart()
    {
        BtnPanel.SetActive(false);
        StartCoroutine(Example_ConfigureTransportAndStartNgoAsHost());
    }
    public void RelayClinetStart()
    {
        StartCoroutine(Example_ConfigreTransportAndStartNgoAsConnectingPlayer());
    }

    async void Example_AuthenticatingAPlayer()
    {
        try
        {
            await UnityServices.InitializeAsync();
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
            var playerID = AuthenticationService.Instance.PlayerId;
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }
    public static async Task<(string ipv4address, ushort port, byte[] allocationIdBytes, byte[] connectionData, byte[] key, string joinCode)> AllocateRelayServerAndGetJoinCode(int maxConnections, string region = null)
    {
        Allocation allocation;
        string createJoinCode;
        try
        {
            allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections, region);
        }
        catch (Exception e)
        {
            Debug.LogError($"Relay create allocation request failed {e.Message}");
            throw;
        }

        Debug.Log($"server: {allocation.ConnectionData[0]} {allocation.ConnectionData[1]}");
        Debug.Log($"server: {allocation.AllocationId}");

        try
        {
            createJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        }
        catch
        {
            Debug.LogError("Relay create join code request failed");
            throw;
        }

        var dtlsEndpoint = allocation.ServerEndpoints.First(e => e.ConnectionType == "dtls");
        return (dtlsEndpoint.Host, (ushort)dtlsEndpoint.Port, allocation.AllocationIdBytes, allocation.ConnectionData, allocation.Key, createJoinCode);
    }
    IEnumerator Example_ConfigureTransportAndStartNgoAsHost()//호스트 시작
    {
        var serverRelayUtilityTask = AllocateRelayServerAndGetJoinCode(m_MaxConnections);
        while (!serverRelayUtilityTask.IsCompleted)
        {
            yield return null;
        }
        if (serverRelayUtilityTask.IsFaulted)
        {
            Debug.LogError("Exception thrown when attempting to start Relay Server. Server not started. Exception: " + serverRelayUtilityTask.Exception.Message);
            yield break;
        }

        var (ipv4address, port, allocationIdBytes, connectionData, key, joinCode) = serverRelayUtilityTask.Result;

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetHostRelayData(ipv4address, port, allocationIdBytes, key, connectionData, true);
        NetworkManager.Singleton.StartHost();
        ShowingJoinCode.text = joinCode;
        RelayJoinCode = joinCode;
        Debug.Log(joinCode);
        yield return null;
    }
    public static async Task<(string ipv4address, ushort port, byte[] allocationIdBytes, byte[] connectionData, byte[] hostConnectionData, byte[] key)> JoinRelayServerFromJoinCode(string joinCode)
    {
        JoinAllocation allocation;
        try
        {
            allocation = await RelayService.Instance.JoinAllocationAsync(joinCode);
        }
        catch
        {
            Debug.LogError("Relay join request failed");
            throw;
        }

        Debug.Log($"client connection data: {allocation.ConnectionData[0]} {allocation.ConnectionData[1]}");
        Debug.Log($"host connection data: {allocation.HostConnectionData[0]} {allocation.HostConnectionData[1]}");
        Debug.Log($"client allocation ID: {allocation.AllocationId}");

        var dtlsEndpoint = allocation.ServerEndpoints.First(e => e.ConnectionType == "dtls");
        return (dtlsEndpoint.Host, (ushort)dtlsEndpoint.Port, allocation.AllocationIdBytes, allocation.ConnectionData, allocation.HostConnectionData, allocation.Key);
    }
    IEnumerator Example_ConfigreTransportAndStartNgoAsConnectingPlayer()//클라이언트 시작
    {
        var clientRelayUtilityTask = JoinRelayServerFromJoinCode(RelayJoinCode);

        while (!clientRelayUtilityTask.IsCompleted)
        {
            yield return null;
        }

        if (clientRelayUtilityTask.IsFaulted)
        {
            Debug.LogError("Exception thrown when attempting to connect to Relay Server. Exception: " + clientRelayUtilityTask.Exception.Message);
            yield break;
        }

        var (ipv4address, port, allocationIdBytes, connectionData, hostConnectionData, key) = clientRelayUtilityTask.Result;

        NetworkManager.Singleton.GetComponent<UnityTransport>().SetClientRelayData(ipv4address, port, allocationIdBytes, key, connectionData, hostConnectionData, true);
        NetworkManager.Singleton.StartClient();
        yield return null;
    }
    public void JoinCodeInputFieldActivation()
    {
        RelayJoinCode = inputField.text;
        if (RelayJoinCode != "")
        {
            inputFieldObject.SetActive(false);
            RelayClinetStart();
        }
        else
        {
            ShowingJoinCode.text = "This is incorrect information.";
        }
    }
    public void JoinCodeBtn()
    {
        inputFieldObject.SetActive(true);
        BtnPanel.SetActive(false);
    }
}


