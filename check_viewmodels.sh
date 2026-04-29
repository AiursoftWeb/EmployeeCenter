#!/bin/bash
for file in $(find src/Aiursoft.EmployeeCenter/Models -name "*ViewModel.cs"); do
    # Check if the class inherits from UiStackLayoutViewModel or similar, or is just a ViewModel
    if ! grep -q "PageTitle" "$file"; then
        echo "Missing PageTitle: $file"
    fi
done
