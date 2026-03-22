using System.Net;
using System.Net.Sockets;
using Lextm.SharpSnmpLib;
using Lextm.SharpSnmpLib.Messaging;
using Lextm.SharpSnmpLib.Security;

namespace SnmpSimulator;

public class SnmpAgent
{
    private static readonly UserRegistry Users = new();
    private readonly IPEndPoint _endpoint;
    private readonly SnmpStore _store;
    private readonly UdpClient _udp;
    private readonly string _readCommunity;
    private readonly string _writeCommunity;

    public SnmpAgent(string ip, int port, SnmpStore store, string readCommunity, string writeCommunity)
    {
        _endpoint = new IPEndPoint(IPAddress.Parse(ip), port);
        _store = store;
        _udp = new UdpClient(_endpoint);
        _readCommunity = readCommunity;
        _writeCommunity = writeCommunity;
    }

    public async Task StartAsync()
    {
        Console.WriteLine($"SNMP Agent listening on {_endpoint}");

        while (true)
        {
            var result = await _udp.ReceiveAsync();

            var messages = MessageFactory.ParseMessages(result.Buffer, Users);
            if (messages.Count == 0)
            {
                continue;
            }

            var request = messages[0];
            var community = request.Parameters.UserName.ToString();

            if (request.Pdu().TypeCode == SnmpType.GetRequestPdu)
            {
                if (CanRead(community))
                {
                    await HandleGet(request, result.RemoteEndPoint);
                }

                continue;
            }

            if (request.Pdu().TypeCode == SnmpType.GetNextRequestPdu)
            {
                if (CanRead(community))
                {
                    await HandleGetNext(request, result.RemoteEndPoint);
                }

                continue;
            }

            if (request.Pdu().TypeCode == SnmpType.GetBulkRequestPdu)
            {
                if (CanRead(community))
                {
                    await HandleGetBulk(request, result.RemoteEndPoint);
                }

                continue;
            }

            if (request.Pdu().TypeCode == SnmpType.SetRequestPdu)
            {
                if (CanWrite(community))
                {
                    await HandleSet(request, result.RemoteEndPoint);
                }
            }
        }
    }

    private bool CanRead(string community)
    {
        return string.Equals(community, _readCommunity, StringComparison.Ordinal) ||
               string.Equals(community, _writeCommunity, StringComparison.Ordinal);
    }

    private bool CanWrite(string community)
    {
        return string.Equals(community, _writeCommunity, StringComparison.Ordinal);
    }
    
    private async Task HandleGet(ISnmpMessage request, IPEndPoint sender)
    {
        var variables = new List<Variable>();

        foreach (var v in request.Pdu().Variables)
        {
            var obj = _store.Get(v.Id.ToString());

            if (obj != null)
                variables.Add(new Variable(v.Id, obj.Value));
            else
                variables.Add(new Variable(v.Id, new NoSuchObject()));
        }

        var response = new ResponseMessage(
            request.RequestId(),
            request.Version,
            request.Parameters.UserName,
            ErrorCode.NoError,
            0,
            variables);

        await _udp.SendAsync(response.ToBytes(), sender);
    }

    private async Task HandleGetNext(ISnmpMessage request, IPEndPoint sender)
    {
        var variables = new List<Variable>();

        foreach (var v in request.Pdu().Variables)
        {
            var nextObj = _store.GetNext(v.Id.ToString());

            if (nextObj is null)
            {
                variables.Add(new Variable(v.Id, new EndOfMibView()));
                continue;
            }

            variables.Add(new Variable(new ObjectIdentifier(nextObj.Oid), nextObj.Value));
        }

        var response = new ResponseMessage(
            request.RequestId(),
            request.Version,
            request.Parameters.UserName,
            ErrorCode.NoError,
            0,
            variables);

        await _udp.SendAsync(response.ToBytes(), sender);
    }

    private async Task HandleGetBulk(ISnmpMessage request, IPEndPoint sender)
    {
        var pdu = request.Pdu();
        var requestVariables = pdu.Variables;
        var variables = new List<Variable>();

        var nonRepeaters = Math.Max(0, pdu.ErrorStatus.ToInt32());
        var maxRepetitions = Math.Max(0, pdu.ErrorIndex.ToInt32());
        nonRepeaters = Math.Min(nonRepeaters, requestVariables.Count);

        for (var i = 0; i < nonRepeaters; i++)
        {
            AddNextOrEnd(variables, requestVariables[i].Id.ToString());
        }

        for (var i = nonRepeaters; i < requestVariables.Count; i++)
        {
            var cursorOid = requestVariables[i].Id.ToString();
            var endReached = false;

            for (var repetition = 0; repetition < maxRepetitions; repetition++)
            {
                if (endReached)
                {
                    variables.Add(new Variable(new ObjectIdentifier(cursorOid), new EndOfMibView()));
                    continue;
                }

                var nextObj = _store.GetNext(cursorOid);
                if (nextObj is null)
                {
                    variables.Add(new Variable(new ObjectIdentifier(cursorOid), new EndOfMibView()));
                    endReached = true;
                    continue;
                }

                variables.Add(new Variable(new ObjectIdentifier(nextObj.Oid), nextObj.Value));
                cursorOid = nextObj.Oid;
            }
        }

        var response = new ResponseMessage(
            request.RequestId(),
            request.Version,
            request.Parameters.UserName,
            ErrorCode.NoError,
            0,
            variables);

        await _udp.SendAsync(response.ToBytes(), sender);
    }

    private void AddNextOrEnd(ICollection<Variable> variables, string requestedOid)
    {
        var nextObj = _store.GetNext(requestedOid);

        if (nextObj is null)
        {
            variables.Add(new Variable(new ObjectIdentifier(requestedOid), new EndOfMibView()));
            return;
        }

        variables.Add(new Variable(new ObjectIdentifier(nextObj.Oid), nextObj.Value));
    }
    
    private async Task HandleSet(ISnmpMessage request, IPEndPoint sender)
    {
        var variables = new List<Variable>();
        var errorCode = ErrorCode.NoError;
        var errorIndex = 0;

        for (var index = 0; index < request.Pdu().Variables.Count; index++)
        {
            var v = request.Pdu().Variables[index];
            var setResult = _store.Set(v.Id.ToString(), v.Data);

            if (setResult == SnmpSetResult.NotFound)
            {
                if (errorCode == ErrorCode.NoError)
                {
                    errorCode = ErrorCode.NoCreation;
                    errorIndex = index + 1;
                }

                variables.Add(v);
                continue;
            }

            if (setResult == SnmpSetResult.NotWritable)
            {
                if (errorCode == ErrorCode.NoError)
                {
                    errorCode = ErrorCode.NotWritable;
                    errorIndex = index + 1;
                }

                variables.Add(v);
                continue;
            }

            Console.WriteLine($"SET {v.Id} = {v.Data}");

            variables.Add(new Variable(v.Id, v.Data));
        }

        var response = new ResponseMessage(
            request.RequestId(),
            request.Version,
            request.Parameters.UserName,
            errorCode,
            errorIndex,
            variables);

        await _udp.SendAsync(response.ToBytes(), sender);
    }
}