namespace CosmosOptimize
{
    public class Mentor

    {
        public string id  => MentorId;
        public string MentorId { get; set; }
        public string MentorName { get; set; }        
    }

    public class Class{

        public string id  => ClassId;
        public string ClassId { get; set; }
        public string ClassName { get; set; }
        public string MentorId { get; set; }
    }
}