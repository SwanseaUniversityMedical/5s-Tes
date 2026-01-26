using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FiveSafesTes.Core.Models.Enums;
using FiveSafesTes.Core.Models.Helpers;

namespace FiveSafesTes.Core.Models
{
    public class SubmissionGetProjectModel
    {

        public int Id { get; set; }

        public string FormData { get; set; }
        public string Name { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime StartDate { get; set; }
        public string ProjectDescription { get; set; }

        public string? ProjectOwner { get; set; }
        public string? ProjectContact { get; set; }
        public string? SubmissionBucket { get; set; }
        public string? OutputBucket { get; set; }
        public virtual List<UserGetProjectModel> Users { get; set; }

        public virtual List<TreGetProjectModel> Tres { get; set; }

        public virtual List<UserGetProjectModel> UsersNotInProject { get; set; }

        public virtual List<TreGetProjectModel> TresNotInProject { get; set; }

        public virtual List<SubmissionsGetProjectModel> Submissions { get; set; }
        public SubmissionGetProjectModel()
        {

        }

        public SubmissionGetProjectModel(Project Project, DbSet<User> UsersAll, DbSet<Tre> TREsALL)
        {
            Id = Project.Id;
            FormData = Project.FormData;
            Name = Project.Name;
            EndDate = Project.EndDate;
            StartDate = Project.StartDate;
            ProjectDescription = Project.ProjectDescription;
            ProjectOwner = Project.ProjectOwner;
            ProjectContact = Project.ProjectContact;
            SubmissionBucket = Project.SubmissionBucket;
            OutputBucket = Project.OutputBucket;
            Users = new List<UserGetProjectModel>();
            UsersNotInProject = new List<UserGetProjectModel>();
           

            foreach (var user in UsersAll.ToArray())
            {
                if (Project.Users.Contains(user))
                {
                    Users.Add(new UserGetProjectModel(user));
                }
                else
                {
                    UsersNotInProject.Add(new UserGetProjectModel(user));
                }
            }
            

            Tres = new List<TreGetProjectModel>();
            TresNotInProject = new List<TreGetProjectModel>();
    

            foreach (var tre in TREsALL.ToArray())
            {
                if (Project.Tres.Contains(tre))
                {
                    Tres.Add(new TreGetProjectModel(tre, Id));
                }
                else
                {
                    TresNotInProject.Add(new TreGetProjectModel(tre, Id));
                }
            }
     
            Submissions = new List<SubmissionsGetProjectModel>();
            foreach (var item in Project.Submissions)
            {
                Submissions.Add(new SubmissionsGetProjectModel(item));
            }

        }
    }

    public class SubmissionsGetProjectModel
    {

        public bool HasParent { get; set; }
        public int Id { get; set; }
        public int? ParentId { get; set; }

        public StatusType Status { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime StartTime { get; set; }
        public string TesName { get; set; }
        public string ProjectName { get; set; }
        public string? SubmittedByName { get; set; }

        public string GetFormattedStartDate()
        {
            var date = StartTime.ToString("yyyy/MM/dd:HH:mm:ss");
            return date;
        }

        public string GetTotalDisplayTime()
        {
            var end = EndTime == DateTime.MinValue ? (DateTime.Now).ToUniversalTime() : EndTime;
            var data = TimeHelper.GetDisplayTime(StartTime, end);
            return data;
        }

        public SubmissionsGetProjectModel()
        {

        }

        public SubmissionsGetProjectModel(Submission Submission)
        {
            this.HasParent = Submission.Parent != null;
            this.Id = Submission.Id;
            this.Status = Submission.Status;
            this.ParentId = Submission.Parent?.Id;
            this.EndTime = Submission.EndTime;
            this.StartTime = Submission.StartTime;
            this.TesName = Submission.TesName;
            this.ProjectName = Submission.Project.Name;
            this.SubmittedByName = Submission.SubmittedBy?.Name;
        }

    }

    public class UserGetProjectModel
    {
        public int Id { get; set; }
        public string? Name { get; set; }

        public string? FullName { get; set; }


        public UserGetProjectModel()
        {

        }
        public UserGetProjectModel(User User)
        {
            Id = User.Id;
            Name = User.Name;
            FullName = User.FullName;
        }

    }


    public class TreGetProjectModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public TreDecisionsGetProjectModel ProjectTreDecision;

        public TreGetProjectModel()
        {

        }
        public TreGetProjectModel(Tre Tre, int ProjectId, bool DoProjectTreDecision = true)
        {
            Id = Tre.Id;
            Name = Tre.Name;
        
            if (DoProjectTreDecision)
            {
                ProjectTreDecision = new TreDecisionsGetProjectModel();
                var localdec = Tre.ProjectTreDecisions
                    .Where(x => x.SubmissionProj != null && x.SubmissionProj.Id == ProjectId).MaxBy(x => x.Id);
                if (localdec != null)
                {
                    Decision decision = localdec.Decision;
                    ProjectTreDecision.Decision = decision;
                }
                
            }
          
        }
    }

    public class TreDecisionsGetProjectModel
    {
        public Decision Decision { get; set; }


    }
}
