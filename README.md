![Icon](https://raw.github.com/Fody/Validar/master/Icons/package_icon.png)

## This is an add-in for [Fody](https://github.com/Fody/Fody/) 

Provides validation for XAML binding models.

Injects [IDataErrorInfo](http://msdn.microsoft.com/en-us/library/system.componentmodel.IDataErrorInfo.aspx) or [INotifyDataErrorInfo](http://msdn.microsoft.com/en-us/library/system.componentmodel.INotifyDataErrorInfo.aspx) code into a class at compile time.

[Introduction to Fody](http://github.com/Fody/Fody/wiki/SampleUsage)

## Nuget package

http://nuget.org/packages/Validar.Fody 

## Your Model Code

 * Must implement `INotifyPropertyChanged` (in this case implementation excluded for brevity).
 * Contain a `[InjectValidation]` attribute.

For example

    [InjectValidation]
    public class Person : INotifyPropertyChanged
    {
        public string GivenNames { get; set; }
        public string FamilyName { get; set; }
    }

### Your validation template code

    public class ValidationTemplate : IDataErrorInfo, INotifyDataErrorInfo
    {
        public ValidationTemplate(INotifyPropertyChanged target)
        {
            // Provide your own implementation
        }

        // Your implementation of IDataErrorInfo
        // Your implementation of INotifyDataErrorInfo
    }


### What gets compiled

Note that an instance of `ValidationTemplate` has been injected into `Person` 

    public class Person : INotifyPropertyChanged, IDataErrorInfo, INotifyDataErrorInfo 
    {
        IDataErrorInfo validationTemplate;
        public string GivenNames { get; set; }
        public string FamilyName { get; set; }

        public Person()
        {
            validationTemplate = new ValidationTemplate(this);
        }

        // Your implementation of IDataErrorInfo
        // Your implementation of INotifyDataErrorInfo
    }

        
## Validation Template

 * Must be named `ValidationTemplate`.
 * Namespace doesn't matter.
 * Must implement either `IDataErrorInfo` or `INotifyDataErrorInfo` or both.
 * Have a instance constructor that takes a `INotifyPropertyChanged`.
 * Can be generic e.g. `ValidationTemplate<T> where T: INotifyPropertyChanged`

### Current Assembly

If `ValidationTemplate`  exist in the current assembly they will be picked up automatically.

### Other Assembly

If `ValidationTemplate`  exist in a different assembly You will need to use a `[ValidationTemplateAttribute]` to tell Validar where to look.

    [assembly: ValidationTemplateAttribute(typeof(MyUtilsLibrary.ValidationTemplate))]
    
## Validation Template Implementations

You can implement `ValidationTemplate` in any way you want. Here are some suggested implementations that will allow you to leverage common validation libraries. 

### [FluentValidation](https://github.com/JeremySkinner/FluentValidation)

    Install-Package FluentValidation

Note that FluentValidation extracts the model validation into a different class

    public class PersonValidator : AbstractValidator<Person>
    {
        public PersonValidator()
        {
            RuleFor(x => x.FamilyName).NotEmpty();
            RuleFor(x => x.GivenNames).NotEmpty();
        }
    }
    
    public class ValidationTemplate : IDataErrorInfo, INotifyDataErrorInfo
    {
        INotifyPropertyChanged target;
        IValidator validator;
        ValidationResult validationResult;
        static ConcurrentDictionary<RuntimeTypeHandle, IValidator> validators = new ConcurrentDictionary<RuntimeTypeHandle, IValidator>();

        public ValidationTemplate(INotifyPropertyChanged target)
        {
            this.target = target;
            validator = GetValidator(target.GetType());
            validationResult = validator.Validate(target);
            target.PropertyChanged += Validate;
        }

        static IValidator GetValidator(Type modelType)
        {
            IValidator validator;
            if (!validators.TryGetValue(modelType.TypeHandle, out validator))
            {
                var typeName = string.Format("{0}.{1}Validator", modelType.Namespace, modelType.Name);
                var type = modelType.Assembly.GetType(typeName, true);
                validators[modelType.TypeHandle] = validator = (IValidator)Activator.CreateInstance(type);
            }
            return validator;
        }

        void Validate(object sender, PropertyChangedEventArgs e)
        {
            validationResult = validator.Validate(target);
            foreach (var error in validationResult.Errors)
            {
                RaiseErrorsChanged(error.PropertyName);
            }
        }

        public IEnumerable GetErrors(string propertyName)
        {
            return validationResult.Errors
                                   .Where(x => x.PropertyName == propertyName)
                                   .Select(x => x.ErrorMessage);
        }

        public bool HasErrors
        {
            get { return validationResult.Errors.Count > 0; }
        }

        public string Error
        {
            get
            {
                var strings = validationResult.Errors.Select(x => x.ErrorMessage)
                                              .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public string this[string propertyName]
        {
            get
            {
                var strings = validationResult.Errors.Where(x => x.PropertyName == propertyName)
                                              .Select(x => x.ErrorMessage)
                                              .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        void RaiseErrorsChanged(string propertyName)
        {
            var handler = ErrorsChanged;
            if (handler != null)
            {
                handler(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }
    }

### [Sandra.SimpleValidator](https://github.com/phillip-haydon/Sandra.SimpleValidator)

    Install-Package Sandra.SimpleValidator

Note that Sandra.SimpleValidator extracts the model validation into a different class

    public class PersonValidator : ValidateThis<Person>
    {
        public PersonValidator()
        {
            For(x => x.GivenNames)
                .Ensure(new Required().WithMessage("'Given Names' should not be empty."));

            For(x => x.FamilyName)
                .Ensure(new Required().WithMessage("'Family Name' should not be empty."));
        }
    }

    public class ValidationTemplate : IDataErrorInfo, INotifyDataErrorInfo
    {
        INotifyPropertyChanged target;
        IModelValidator validator;
        ValidationResult validationResult;

        public ValidationTemplate(INotifyPropertyChanged target)
        {
            this.target = target;
            validator = GetValidator(target.GetType());
            validationResult = validator.Validate(target);
            target.PropertyChanged += Validate;
        }

        static ConcurrentDictionary<RuntimeTypeHandle, IModelValidator> validators = new ConcurrentDictionary<RuntimeTypeHandle, IModelValidator>();

        static IModelValidator GetValidator(Type modelType)
        {
            IModelValidator validator;
            if (!validators.TryGetValue(modelType.TypeHandle, out validator))
            {
                var typeName = string.Format("{0}.{1}Validator", modelType.Namespace, modelType.Name);
                var type = modelType.Assembly.GetType(typeName, true);
                validators[modelType.TypeHandle] = validator = (IModelValidator)Activator.CreateInstance(type);
            }
            return validator;
        }

        void Validate(object sender, PropertyChangedEventArgs e)
        {
            validationResult = validator.Validate(target);
            foreach (var error in validationResult.Messages)
            {
                RaiseErrorsChanged(error.PropertyName);
            }
        }

        public IEnumerable GetErrors(string propertyName)
        {
            return validationResult.Messages
                                   .Where(x => x.PropertyName == propertyName)
                                   .Select(x => x.Message);
        }

        public bool HasErrors
        {
            get { return validationResult.IsInvalid; }
        }

        public string Error
        {
            get
            {
                var strings = validationResult.Messages.Select(x => x.Message)
                                              .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public string this[string propertyName]
        {
            get
            {
                var strings = validationResult.Messages.Where(x => x.PropertyName == propertyName)
                                              .Select(x => x.Message)
                                              .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        void RaiseErrorsChanged(string propertyName)
        {
            var handler = ErrorsChanged;
            if (handler != null)
            {
                handler(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }
    }
    


### [DataAnnotations](http://msdn.microsoft.com/en-us/library/system.componentmodel.dataannotations.aspx)

    public class ValidationTemplate :
        IDataErrorInfo, 
        INotifyDataErrorInfo
    {
        INotifyPropertyChanged target;
        ValidationContext validationContext;
        List<ValidationResult> validationResults;

        public ValidationTemplate(INotifyPropertyChanged target)
        {
            this.target = target;
            validationContext = new ValidationContext(target, null, null);
            validationResults = new List<ValidationResult>();
            Validator.TryValidateObject(target, validationContext, validationResults, true);
            target.PropertyChanged += Validate;
        }

        void Validate(object sender, PropertyChangedEventArgs e)
        {
            validationResults.Clear();
            Validator.TryValidateObject(target, validationContext, validationResults, true);
            var hashSet = new HashSet<string>(validationResults.SelectMany(x => x.MemberNames));
            foreach (var error in hashSet)
            {
                RaiseErrorsChanged(error);
            }
        }

        public IEnumerable GetErrors(string propertyName)
        {
            return validationResults.Where(x => x.MemberNames.Contains(propertyName))
                                    .Select(x => x.ErrorMessage);
        }

        public bool HasErrors
        {
            get { return validationResults.Count > 0; }
        }

        public string Error
        {
            get
            {
                var strings = validationResults.Select(x => x.ErrorMessage)
                                               .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public string this[string propertyName]
        {
            get
            {
                var strings = validationResults.Where(x => x.MemberNames.Contains(propertyName))
                                               .Select(x => x.ErrorMessage)
                                               .ToArray();
                return string.Join(Environment.NewLine, strings);
            }
        }

        public event EventHandler<DataErrorsChangedEventArgs> ErrorsChanged;

        void RaiseErrorsChanged(string propertyName)
        {
            var handler = ErrorsChanged;
            if (handler != null)
            {
                handler(this, new DataErrorsChangedEventArgs(propertyName));
            }
        }
    }

## Icon

<a href="http://thenounproject.com/noun/check-mark/#icon-No6407" target="_blank">Check Mark</a> designed by <a href="http://thenounproject.com/mateozlatar" target="_blank">Mateo Zlatar</a> from The Noun Project

