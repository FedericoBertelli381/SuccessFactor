namespace SuccessFactor;

public static class SuccessFactorDomainErrorCodes
{
    // Glossario progetto
    public const string TenantMissing = "SuccessFactor:TenantMissing";
    public const string UserNotAuthenticated = "SuccessFactor:UserNotAuthenticated";
    public const string EmployeeNotLinkedToUser = "SuccessFactor:EmployeeNotLinkedToUser";
    public const string ProcessTemplateNotFound = "SuccessFactor:ProcessTemplateNotFound";
    public const string PhaseNotInTemplate = "SuccessFactor:PhaseNotInTemplate";
    public const string CycleNotFound = "SuccessFactor:CycleNotFound";
    public const string ParticipantNotFound = "SuccessFactor:ParticipantNotFound";
    public const string ParticipantHasNoPhase = "SuccessFactor:ParticipantHasNoPhase";
    public const string PhaseEditNotAllowed = "SuccessFactor:PhaseEditNotAllowed";
    public const string PhaseAdvanceNotAllowed = "SuccessFactor:PhaseAdvanceNotAllowed";
    public const string FieldNotEditable = "SuccessFactor:FieldNotEditable";
    public const string GoalAssignmentNotFound = "SuccessFactor:GoalAssignmentNotFound";
    public const string GoalAssignmentClosed = "SuccessFactor:GoalAssignmentClosed";
    public const string ProgressEntryEmpty = "SuccessFactor:ProgressEntryEmpty";
    public const string ScoreOutOfRange = "SuccessFactor:ScoreOutOfRange";
    public const string AssessmentNotFound = "SuccessFactor:AssessmentNotFound";
    public const string AssessmentClosed = "SuccessFactor:AssessmentClosed";
    public const string RequiredCompetenciesMissingScore = "SuccessFactor:RequiredCompetenciesMissingScore";

    // Extra utili per uniformare le BusinessException raw che hai già nel codice
    public const string EmployeeNotFound = "SuccessFactor:EmployeeNotFound";
    public const string EvaluatorNotFound = "SuccessFactor:EvaluatorNotFound";
    public const string CompetencyModelNotFound = "SuccessFactor:CompetencyModelNotFound";
    public const string AssessmentMissingModel = "SuccessFactor:AssessmentMissingModel";
    public const string AssessmentItemNotFound = "SuccessFactor:AssessmentItemNotFound";
    public const string AssessmentNotSubmitted = "SuccessFactor:AssessmentNotSubmitted";
}
