const initializeMailManagerDemo = () => {
  const form = document.querySelector('#kc-form-login form, form#kc-form-login, #kc-form-login');
  const username = document.querySelector('#username');
  const password = document.querySelector('#password');
  const submit = document.querySelector('#kc-login');

  if (document.querySelector('.mm-demo')
      || !(form instanceof HTMLFormElement)
      || !(username instanceof HTMLInputElement)
      || !(password instanceof HTMLInputElement)
      || !(submit instanceof HTMLElement)) return;

  const launchDemo = () => {
    username.value = 'demo';
    password.value = 'demo';
    submit.click();
  };

  const isAutomaticDemoRequest = new URLSearchParams(window.location.search).get('login_hint') === 'demo';
  if (isAutomaticDemoRequest) {
    document.documentElement.classList.add('mm-demo-autologin');
    form.setAttribute('aria-busy', 'true');
    window.setTimeout(launchDemo, 0);
    return;
  }

  const section = document.createElement('section');
  section.className = 'mm-demo';
  section.setAttribute('aria-label', 'Accès de démonstration');
  section.innerHTML = `
    <button class="mm-demo__button" type="button">Découvrir avec le profil démo</button>
    <p class="mm-demo__hint">Aucun compte à créer. Seuls les outils de simulation et les données d’exemple sont accessibles.</p>
  `;

  section.querySelector('button')?.addEventListener('click', launchDemo);

  form.append(section);
};

const initializePasswordToggles = () => {
  document.querySelectorAll('button[data-password-toggle]').forEach((button) => {
    const controlledInput = document.getElementById(button.getAttribute('aria-controls') ?? '');
    if (!(button instanceof HTMLButtonElement) || !(controlledInput instanceof HTMLInputElement)) return;

    const synchronizeState = () => {
      button.classList.toggle('mm-password-visible', controlledInput.type === 'text');
    };

    synchronizeState();
    button.addEventListener('click', () => window.requestAnimationFrame(synchronizeState));
  });
};

const mailManagerAppOrigin = () => {
  const redirectUri = new URLSearchParams(window.location.search).get('redirect_uri');
  if (redirectUri) {
    try {
      const parsed = new URL(redirectUri);
      if (['http:', 'https:'].includes(parsed.protocol)) return parsed.origin;
    } catch {
      // Une URL OAuth invalide est ignorée ; Keycloak la rejettera indépendamment.
    }
  }
  return 'http://localhost:5173';
};

const initializeLegalLinks = () => {
  if (document.querySelector('.mm-legal-links')) return;
  const target = document.querySelector('#kc-content-wrapper, .pf-v5-c-login__main-body, .pf-c-login__main-body, .card-pf');
  if (!(target instanceof HTMLElement)) return;
  const base = mailManagerAppOrigin();
  const language = document.documentElement.lang?.toLowerCase().startsWith('en') ? 'en' : 'fr';
  const labels = language === 'fr'
    ? [['Mentions légales', '/mentions-legales'], ['Confidentialité', '/politique-confidentialite'], ["Conditions d’utilisation", '/conditions-utilisation'], ['Cookies', '/cookies']]
    : [['Legal notice', '/mentions-legales'], ['Privacy', '/politique-confidentialite'], ['Terms of use', '/conditions-utilisation'], ['Cookies', '/cookies']];
  const nav = document.createElement('nav');
  nav.className = 'mm-legal-links';
  nav.setAttribute('aria-label', language === 'fr' ? 'Informations légales' : 'Legal information');
  labels.forEach(([label, path]) => {
    const link = document.createElement('a');
    link.href = `${base}${path}`;
    link.target = '_blank';
    link.rel = 'noreferrer';
    link.textContent = label;
    nav.append(link);
  });
  target.append(nav);
};

const registrationCopy = {
  fr: {
    profile: 'Profil',
    security: 'Sécurité',
    profileTitle: 'Faisons connaissance',
    profileHint: 'Indiquez les informations qui identifieront votre espace Mail Manager.',
    securityTitle: 'Choisissez vos identifiants',
    securityHint: 'Ils vous permettront de retrouver vos boîtes et vos règles en toute sécurité.',
    next: 'Continuer',
    back: 'Retour',
    submit: 'Créer mon compte',
    invalidEmail: 'Saisissez une adresse courriel valide.',
    passwordMismatch: 'Les mots de passe ne correspondent pas.'
  },
  en: {
    profile: 'Profile',
    security: 'Security',
    profileTitle: 'Tell us about yourself',
    profileHint: 'Add the details that will identify your Mail Manager workspace.',
    securityTitle: 'Choose your credentials',
    securityHint: 'Use them to securely access your mailboxes and rules.',
    next: 'Continue',
    back: 'Back',
    submit: 'Create my account',
    invalidEmail: 'Enter a valid email address.',
    passwordMismatch: 'The passwords do not match.'
  }
};

const initializeRegistrationSteps = () => {
  const form = document.querySelector('form#kc-register-form');
  if (!(form instanceof HTMLFormElement) || form.dataset.mmEnhanced === 'true') return;

  const fieldIds = ['firstName', 'lastName', 'email', 'username', 'password', 'password-confirm'];
  const fields = Object.fromEntries(fieldIds.map((id) => [id, document.getElementById(id)]));
  if (fieldIds.some((id) => !(fields[id] instanceof HTMLInputElement))) return;

  const fieldGroup = (id) => fields[id].closest('.pf-v5-c-form__group, .pf-c-form__group');
  const profileGroups = ['firstName', 'lastName', 'email'].map(fieldGroup);
  const securityGroups = ['username', 'password', 'password-confirm'].map(fieldGroup);
  const submitArea = form.querySelector('#kc-form-buttons');
  const submit = submitArea?.querySelector('input[type="submit"], button[type="submit"]');

  if ([...profileGroups, ...securityGroups].some((group) => !group)
      || !(submitArea instanceof HTMLElement)
      || !(submit instanceof HTMLElement)) return;

  const language = document.documentElement.lang?.toLowerCase().startsWith('en') ? 'en' : 'fr';
  const copy = registrationCopy[language];
  const firstField = fields.firstName;
  const securityField = fields.username;
  let currentStep = 1;

  fieldIds.forEach((id) => {
    fields[id].required = true;
  });
  fields.email.inputMode = 'email';

  const progress = document.createElement('ol');
  progress.className = 'mm-register-progress';
  progress.setAttribute('aria-label', language === 'fr' ? "Etapes de l'inscription" : 'Registration steps');
  progress.innerHTML = `
    <li class="mm-register-progress__item" data-step="1"><span>1</span>${copy.profile}</li>
    <li class="mm-register-progress__item" data-step="2"><span>2</span>${copy.security}</li>
  `;

  const createStep = (number, title, hint) => {
    const step = document.createElement('section');
    step.className = 'mm-register-step';
    step.dataset.step = String(number);
    step.setAttribute('aria-labelledby', `mm-register-step-${number}-title`);
    step.innerHTML = `
      <header class="mm-register-step__header">
        <h2 id="mm-register-step-${number}-title">${title}</h2>
        <p>${hint}</p>
      </header>
    `;
    return step;
  };

  const profileStep = createStep(1, copy.profileTitle, copy.profileHint);
  const securityStep = createStep(2, copy.securityTitle, copy.securityHint);
  const anchor = profileGroups[0];
  form.insertBefore(progress, anchor);
  form.insertBefore(profileStep, anchor);
  form.insertBefore(securityStep, anchor);
  profileGroups.forEach((group) => profileStep.append(group));
  securityGroups.forEach((group) => securityStep.append(group));

  const legalNotice = document.createElement('p');
  legalNotice.className = 'mm-registration-legal';
  const appOrigin = mailManagerAppOrigin();
  legalNotice.innerHTML = language === 'fr'
    ? `Avant le premier accès, vous devrez accepter les <a href="${appOrigin}/conditions-utilisation" target="_blank" rel="noreferrer">conditions d’utilisation</a> et reconnaître avoir lu la <a href="${appOrigin}/politique-confidentialite" target="_blank" rel="noreferrer">politique de confidentialité</a>.`
    : `Before first access, you will need to accept the <a href="${appOrigin}/conditions-utilisation" target="_blank" rel="noreferrer">terms of use</a> and acknowledge the <a href="${appOrigin}/politique-confidentialite" target="_blank" rel="noreferrer">privacy policy</a>.`;
  securityStep.append(legalNotice);

  const nextArea = document.createElement('div');
  nextArea.className = 'mm-register-actions mm-register-actions--next';
  nextArea.innerHTML = `<button class="mm-register-next" type="button">${copy.next}<span aria-hidden="true">&rarr;</span></button>`;
  profileStep.append(nextArea);

  const back = document.createElement('button');
  back.className = 'mm-register-back';
  back.type = 'button';
  back.innerHTML = `<span aria-hidden="true">&larr;</span>${copy.back}`;
  submitArea.classList.add('mm-register-actions');
  submitArea.prepend(back);
  securityStep.append(submitArea);

  if (submit instanceof HTMLInputElement) submit.value = copy.submit;
  else submit.textContent = copy.submit;

  form.classList.add('mm-register');
  form.dataset.mmEnhanced = 'true';

  const progressItems = [...progress.querySelectorAll('.mm-register-progress__item')];
  const showStep = (number, shouldFocus = true, animate = true) => {
    const direction = number > currentStep ? 'forward' : 'backward';
    currentStep = number;
    profileStep.hidden = number !== 1;
    securityStep.hidden = number !== 2;
    const visibleStep = number === 1 ? profileStep : securityStep;
    visibleStep.classList.remove('is-entering-forward', 'is-entering-backward');
    if (animate) {
      visibleStep.classList.add(`is-entering-${direction}`);
      visibleStep.addEventListener('animationend', () => {
        visibleStep.classList.remove('is-entering-forward', 'is-entering-backward');
      }, { once: true });
    }
    progressItems.forEach((item) => {
      const itemStep = Number(item.dataset.step);
      item.classList.toggle('is-active', itemStep === number);
      item.classList.toggle('is-complete', itemStep < number);
      if (itemStep === number) item.setAttribute('aria-current', 'step');
      else item.removeAttribute('aria-current');
    });
    if (shouldFocus) (number === 1 ? firstField : securityField).focus();
  };

  const validateProfile = () => {
    const emailValue = fields.email.value.trim();
    const emailIsValid = /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(emailValue);
    fields.email.setCustomValidity(emailValue && !emailIsValid ? copy.invalidEmail : '');
    for (const id of ['firstName', 'lastName', 'email']) {
      if (!fields[id].checkValidity()) {
        fields[id].reportValidity();
        fields[id].focus();
        return false;
      }
    }
    return true;
  };

  nextArea.querySelector('button')?.addEventListener('click', () => {
    if (validateProfile()) showStep(2);
  });
  back.addEventListener('click', () => showStep(1));

  fields.email.addEventListener('input', () => {
    fields.email.setCustomValidity('');
  });

  fields['password-confirm'].addEventListener('input', () => {
    fields['password-confirm'].setCustomValidity('');
  });

  form.addEventListener('submit', (event) => {
    if (currentStep === 1) {
      event.preventDefault();
      if (validateProfile()) showStep(2);
      return;
    }

    fields['password-confirm'].setCustomValidity(
      fields.password.value === fields['password-confirm'].value ? '' : copy.passwordMismatch
    );
    if (!form.checkValidity()) {
      event.preventDefault();
      form.reportValidity();
    }
  });

  const invalidField = fieldIds
    .map((id) => fields[id])
    .find((field) => field.getAttribute('aria-invalid') === 'true'
      || field.closest('.pf-v5-c-form__group, .pf-c-form__group')?.querySelector('[aria-live]')?.textContent.trim());
  const initialStep = invalidField && ['username', 'password', 'password-confirm'].includes(invalidField.id) ? 2 : 1;
  showStep(initialStep, false, false);

  window.addEventListener('pageshow', (event) => {
    if (event.persisted && submit instanceof HTMLInputElement) submit.value = copy.submit;
  });
};

const initializeMailManagerTheme = () => {
  initializeMailManagerDemo();
  initializePasswordToggles();
  initializeRegistrationSteps();
  initializeLegalLinks();
};

if (document.readyState === 'loading') {
  document.addEventListener('DOMContentLoaded', initializeMailManagerTheme, { once: true });
} else {
  initializeMailManagerTheme();
}
