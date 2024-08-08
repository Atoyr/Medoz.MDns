// See https://aka.ms/new-console-template for more information
var mdnsService = new Medoz.MDns.MDnsService();
mdnsService.SendMDnsQuery("_airplay._tcp.local");
mdnsService.ListenForResponses();
