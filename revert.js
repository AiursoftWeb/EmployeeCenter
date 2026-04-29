const fs = require('fs');

const filesToRevert = {
    'src/Aiursoft.EmployeeCenter/Models/WeeklyReportViewModels/WeeklyReportRequirementViewModel.cs': ['WeeklyReportRequirementViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/WeeklyReportViewModels/CreateViewModel.cs': ['CreateViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/UsersViewModels/UserWithRolesViewModel.cs': ['UserWithRolesViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/UsersViewModels/UserRoleViewModel.cs': ['UserRoleViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/RolesViewModels/RoleClaimViewModel.cs': ['RoleClaimViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/ReimbursementViewModels/ReimbursementViewModels.cs': ['ActionViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/PayrollViewModels/PayrollExportViewModel.cs': ['PayrollExportViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/PasswordsViewModels/AddShareViewModel.cs': ['AddShareViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/ManageViewModels/SwitchThemeViewModel.cs': ['SwitchThemeViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/InvoiceViewModels/InvoiceItemViewModel.cs': ['InvoiceItemViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/GlobalSettingsViewModels/SettingViewModel.cs': ['SettingViewModel'],
    'src/Aiursoft.EmployeeCenter/Models/GlobalSettingsViewModels/EditViewModel.cs': ['EditViewModel']
};

for (const [file, classes] of Object.entries(filesToRevert)) {
    let content = fs.readFileSync(file, 'utf-8');
    for (const className of classes) {
        // Regex to match the added constructor exactly as it was generated:
        //     public ClassName()
        //     {
        //         PageTitle = "...";
        //     }
        const regex = new RegExp(`\\s*public\\s+${className}\\s*\\(\\)\\s*\\{\\s*PageTitle\\s*=\\s*"[^"]+";\\s*\\}`, 'g');
        content = content.replace(regex, '');
    }
    fs.writeFileSync(file, content);
}
console.log("Reverted");
