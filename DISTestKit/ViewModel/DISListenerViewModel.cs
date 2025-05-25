using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using OpenDis.Dis1998;

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
            _udpClient = new UdpClient(3000);
        }

        public void Start()
        {
            _udpClient.BeginReceive(ReceiveCallback, null);
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            
            try
            {
                IPEndPoint ep = new IPEndPoint(IPAddress.Any, _port);
                byte[] data = _udpClient.EndReceive(ar, ref ep);

                // Continue receiving before processing to prevent missing packets
                _udpClient.BeginReceive(ReceiveCallback, null);

                if (data != null)
                {
                    foreach (var item in data)
                    {
                        // Try to parse the PDU
                        var pdu = PduFactory.CreatePdu(item);
                        if (pdu != null)
                        {
                            // Filter for only EntityStatePdu
                            if (pdu is EntityStatePdu)
                            {
                                // Update chart data in the ViewModel
                                _viewModel.IncrementMessageCount();
                            }
                        }
                    }
                }
                

               
            }

            catch (Exception ex)
            {
                // Handle malformed PDU
                Console.WriteLine($"DIS error: {ex.Message}");
            }

            // Continue receiving
            _udpClient.BeginReceive(ReceiveCallback, null);
        }




    }
}
