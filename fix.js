const fs = require('fs');
const path = require('path');

// 1. Remove ViewData["Title"] from .cshtml files
const cshtmlFiles = [
    'src/Aiursoft.EmployeeCenter/Views/Reimbursement/Index.cshtml',
    'src/Aiursoft.EmployeeCenter/Views/Reimbursement/Manage.cshtml',
    'src/Aiursoft.EmployeeCenter/Views/Reimbursement/Details.cshtml',
    'src/Aiursoft.EmployeeCenter/Views/Reimbursement/Edit.cshtml',
    'src/Aiursoft.EmployeeCenter/Views/Reimbursement/Create.cshtml',
    'src/Aiursoft.EmployeeCenter/Views/Jobs/Index.cshtml'
];

for (const file of cshtmlFiles) {
    let content = fs.readFileSync(file, 'utf-8');
    content = content.replace(/^\s*ViewData\["Title"\].*?\n/gm, '');
    fs.writeFileSync(file, content);
}

// 2. Fix specific ViewModels based on ViewData["Title"] values removed
const specificFixes = {
    'src/Aiursoft.EmployeeCenter/Models/ReimbursementViewModels/ReimbursementViewModels.cs': {
        'IndexViewModel': 'My Reimbursements',
        'CreateViewModel': 'New Reimbursement Request',
        'EditViewModel': 'Edit Reimbursement Request',
        'DetailsViewModel': 'Reimbursement Details',
        'ManageIndexViewModel': 'Reimbursement Approval Center'
    },
    'src/Aiursoft.EmployeeCenter/Models/BackgroundJobs/JobsIndexViewModel.cs': {
        'JobsIndexViewModel': 'Background Jobs'
    }
};

function injectPageTitle(content, className, pageTitle) {
    // If it already has PageTitle = , do nothing
    const classRegex = new RegExp(`public class ${className}[\\s\\S]*?\\{`);
    const match = content.match(classRegex);
    if (!match) return content;
    
    const classStart = match.index + match[0].length;
    
    // Check if constructor exists
    const constructorRegex = new RegExp(`public ${className}\\s*\\([^)]*\\)\\s*(?::\\s*base\\([^)]*\\)\\s*)?(?:=>\\s*[^;]+;|\\{[^}]*\\})`, 'g');
    let hasConstructor = false;
    let newContent = content;

    // We can just add a simple parameterless constructor at the beginning of the class
    const constructorMatch = Array.from(content.matchAll(new RegExp(`public ${className}\\s*\\(`, 'g')));
    if (constructorMatch.length > 0) {
        // Constructor exists. Let's find it and inject PageTitle
        // Too complex to parse reliably. Let's just find the constructor body or expression body.
        const exprBodyMatch = new RegExp(`public ${className}\\s*\\([^)]*\\)\\s*=>\\s*([^;]+);`).exec(content);
        if (exprBodyMatch) {
            newContent = content.replace(exprBodyMatch[0], `public ${className}() { PageTitle = "${pageTitle}"; }`);
        } else {
            const blockBodyMatch = new RegExp(`public ${className}\\s*\\([^)]*\\)\\s*\\{`).exec(content);
            if (blockBodyMatch) {
                newContent = content.replace(blockBodyMatch[0], blockBodyMatch[0] + `\n        PageTitle = "${pageTitle}";`);
            }
        }
    } else {
        // No constructor, add one
        newContent = content.slice(0, classStart) + `\n    public ${className}()\n    {\n        PageTitle = "${pageTitle}";\n    }\n` + content.slice(classStart);
    }
    return newContent;
}

for (const [file, fixes] of Object.entries(specificFixes)) {
    let content = fs.readFileSync(file, 'utf-8');
    for (const [className, title] of Object.entries(fixes)) {
        content = injectPageTitle(content, className, title);
    }
    fs.writeFileSync(file, content);
}

// 3. Fix all other ViewModels missing PageTitle
function walk(dir) {
    let results = [];
    const list = fs.readdirSync(dir);
    list.forEach(function(file) {
        file = path.join(dir, file);
        const stat = fs.statSync(file);
        if (stat && stat.isDirectory()) { 
            results = results.concat(walk(file));
        } else { 
            if (file.endsWith('ViewModel.cs') || file.endsWith('ViewModels.cs')) {
                results.push(file);
            }
        }
    });
    return results;
}

const allViewModels = walk('src/Aiursoft.EmployeeCenter/Models');

for (const file of allViewModels) {
    let content = fs.readFileSync(file, 'utf-8');
    let modified = false;

    // Find all classes that inherit from UiStackLayoutViewModel or other ViewModel (except ActionViewModel, etc)
    const classRegex = /public\s+(?:abstract\s+)?class\s+([A-Za-z0-9_]+ViewModel)\s*(?::\s*[A-Za-z0-9_<>]+)?\s*\{/g;
    let match;
    while ((match = classRegex.exec(content)) !== null) {
        const className = match[1];
        
        // Skip if it doesn't inherit from anything and doesn't seem to be a page model, but wait, 
        // to be safe let's check if the file or class already mentions PageTitle.
        // Actually, just checking if PageTitle is set for this class.
        // If the class contains `PageTitle` inside its body, skip.
        // A simple heuristic: if content contains `PageTitle` and we already have it somewhere, maybe skip?
        // Let's just check the class body text roughly
        const classStart = match.index;
        let classEnd = content.indexOf('public class', classStart + 10);
        if (classEnd === -1) classEnd = content.length;
        
        const classBody = content.slice(classStart, classEnd);
        if (classBody.includes('PageTitle')) continue;
        if (!classBody.includes('UiStackLayoutViewModel') && !match[0].includes('ViewModel')) {
             // Not a UiStackLayoutViewModel or derived ViewModel
             // But Wait, what if it derives from something else that derives from UiStackLayoutViewModel?
             // e.g. public class EditViewModel : CreateViewModel
             // We can just add PageTitle to all classes ending in ViewModel if they don't have it.
        }

        // Generate title: "EditViewModel" -> "Edit", "JobsIndexViewModel" -> "Jobs Index"
        let generatedTitle = className.replace('ViewModel', '').replace(/([A-Z])/g, ' $1').trim();
        if (!generatedTitle) {
             const folderName = path.basename(path.dirname(file)).replace('ViewModels', '');
             generatedTitle = folderName.replace(/([A-Z])/g, ' $1').trim();
        }
        
        content = injectPageTitle(content, className, generatedTitle);
        modified = true;
    }
    
    if (modified) {
        fs.writeFileSync(file, content);
    }
}
console.log("Done");
