using System.Collections.Generic;

namespace CosmosOptimize
{
    public class Mentor1

    {
        public string id => MentorId;
        public string MentorId { get; set; }
        public bool IsActive { get; set; }
        public string Picture { get; set; }
        public int Age { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; }
        public string Copmany { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string About { get; set; }
        public string Registered { get; set; }
        public string Type => "Mentor";

    }

    public class Mentor2

    {
        public string id => MentorId;
        public string MentorId { get; set; }
        public bool IsActive { get; set; }
        public string Picture { get; set; }
        public int Age { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; }
        public string Copmany { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string About { get; set; }
        public string Registered { get; set; }
        public string Type => "Mentor";

        public List<Class2> Classes { get; set; }

    }

    public class Mentor3

    {
        public string id => MentorId;
        public string MentorId { get; set; }
        public bool IsActive { get; set; }
        public string Picture { get; set; }
        public int Age { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; }
        public string Copmany { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string About { get; set; }
        public string Registered { get; set; }

        private string Type => "Mentor";

        public List<Class1> Classes { get; set; }

    }

    public class Class1
    {

        public string id => ClassId;
        public string MentorId { get; set; }
        public string ClassId { get; set; }
        public string ClassName { get; set; }
        public string Date { get; set; }
        public int MaxMentees { get; set; }
        public string Address { get; set; }
        public string Type => "Class";


    }

    public class Class2
    {

        public string id => ClassId;
        public string MentorId { get; set; }

        public string ClassId { get; set; }
        public string ClassName { get; set; }
        public string Date { get; set; }
        public int MaxMentees { get; set; }
        public string Address { get; set; }
        public string Type => "Class";
        public List<Registration1> Registrations { get; set; }

    }

    public class Registration1
    {
        public string id => this.RegistrationId;
        public string MentorId { get; set; }
        public string ClassId { get; set; }
        public string RegistrationId { get; set; }
        public int Age { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; }
        public string Company { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Type => "Registration";

    }

    public class Registration2
    {
        public string id => this.RegistrationId;
        public string partitionKey { get; set; }
        public string MentorId { get; set; }
        public string ClassId { get; set; }
        public string RegistrationId { get; set; }
        public string StudentId { get; set; }
        public int Age { get; set; }
        public string Name { get; set; }
        public string Gender { get; set; }
        public string Company { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Type => "Registration";


        public Registration2(string mentorId, string classId)
        {
            var studentId = System.Guid.NewGuid().ToString();
            StudentId = studentId;
            MentorId = mentorId;
            ClassId = classId;
            partitionKey = $"{studentId}_{mentorId}_{classId}";
        }
    }
}