using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HFiles_Backend.Application.DTOs.Labs
{
    public class LocationDetailsResponse
    {
        public string? Status { get; set; }
        public List<PostOffice>? PostOffice { get; set; }  
    }

    public class PostOffice
    {
        public string? Name { get; set; }
        public string? District { get; set; }  
        public string? State { get; set; }  
    }

}
