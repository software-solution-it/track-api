namespace track_api.Models
{

        public class TrackingEvent
        {
            public string Data { get; set; }
            public string Hora { get; set; }
            public string Local { get; set; }
            public string Status { get; set; }
            public List<string> SubStatus { get; set; }
        }

        public class TrackingResponse
        {
            public string Codigo { get; set; }
            public string Host { get; set; }
            public List<TrackingEvent> Eventos { get; set; }
            public double Time { get; set; }
            public int Quantidade { get; set; }
            public string Servico { get; set; }
            public string Ultimo { get; set; }

    }
}
