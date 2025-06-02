using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using OpenDis.Dis1998;
using DISTestKit.ViewModel;

namespace DISTestKit.ViewModel
{
    public class DISListenerViewModel
    {
        private readonly UdpClient _udpClient;
        private readonly ChartViewModel _viewModel;
        private readonly int _port = 3000;

        public DISListenerViewModel(ChartViewModel viewModel)
        {
            _viewModel = viewModel;
            _udpClient = new UdpClient(_port);
        }

        public void Start()
        {
            _udpClient.BeginReceive(ReceiveCallback, null);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            
            try
            {
                IPEndPoint remoteEp = new(IPAddress.Any, _port);
                byte[] data = _udpClient.EndReceive(ar, ref remoteEp!);

                // Continue receiving before processing to prevent missing packets
                _udpClient.BeginReceive(ReceiveCallback, null);

                if (data != null && data.Length > 0)
                {
                    foreach (var item in data)
                    {
                        var pdu = PduFactory.CreatePdu(item);
                        if (pdu is not null && pdu is EntityStatePdu espdu)
                        {
                            _viewModel.IncrementMessageCount();
                            string src, dest;
                            try
                                {
                                    src = $"{espdu.EntityID.Site}-{espdu.EntityID.Application}-{espdu.EntityID.Entity}";
                                }
                            catch
                                {
                                    src = remoteEp.ToString();
                                }
                            try
                                {
                                    dest = $"(dest:{espdu.EntityID.Site}-{espdu.EntityID.Application})";
                                }
                            catch
                                {
                                    dest = "(dest:unknown)";
                                }
                    
                            string proto = "EntityStatePdu";
                            int length = data.Length;
                            string info = $"Entity X={espdu.EntityLinearVelocity.X:F2}, Y={espdu.EntityLinearVelocity.Y:F2}, Z={espdu.EntityLinearVelocity.Z:F2}";
                            _viewModel.AddPacket(
                                source: src,
                                destination: dest,
                                protocol: proto,
                                length: length,
                                info: info
                            );
                        }
                    }
                }
                
            }

            catch (Exception ex)
            {
                // Handle malformed PDU
                Console.WriteLine($"DIS error: {ex.Message}");
            }
        }




    }
}
