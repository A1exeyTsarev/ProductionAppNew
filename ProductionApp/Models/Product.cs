using System;
using System.Collections.ObjectModel;

namespace ProductionApp.Models
{
    public class Product
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public string SerialNumber { get; set; } = "";
        public DateTime ReceivedDate { get; set; }
        public string MalfunctionDesc { get; set; } = "";
        public string ActFilePath { get; set; } = "";
        public string ExternalInspection { get; set; } = "";
        public string FaultDetection { get; set; } = "";

        public ObservableCollection<ProductComponent> Components { get; set; } = new ObservableCollection<ProductComponent>();
        public ObservableCollection<ProductServiceWork> ServiceWorks { get; set; } = new ObservableCollection<ProductServiceWork>();
    }
}
