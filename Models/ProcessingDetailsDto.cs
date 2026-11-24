using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace comply_flow_api.Models;

public class ProcessingDetailsDto
{
   public Processing? Processing { get; set; } = null!;

   public Configuration? Configuration { get; set; } = null!;

   public PreProcessing? Preprocessing { get; set; } = null!;

   public Rules? Rule { get; set; } = null!;

   public Message? Message { get; set; } = null!;    

   public Response? Response { get; set; } = null!;  

}